using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToolboxManager.Api.Data;
using ToolboxManager.Api.Dtos;
using ToolboxManager.Api.Models;

namespace ToolboxManager.Api.Controllers;

[ApiController]
[Route("api/applications")]
public sealed class ApplicationsController : ControllerBase
{
    private readonly ToolboxDbContext _db;
    private readonly ILogger<ApplicationsController> _logger;

    public ApplicationsController(ToolboxDbContext db, ILogger<ApplicationsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApplicationSummaryDto>>> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var query = _db.Applications.AsNoTracking();
        if (!includeInactive)
            query = query.Where(a => a.IsActive);

        var rows = await query
            .OrderBy(a => a.Name)
            .Select(a => new
            {
                App = a,
                ParameterCount = a.Parameters.Count(),
            })
            .ToListAsync(ct);

        return Ok(rows.Select(r => r.App.ToSummaryDto(r.ParameterCount)).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApplicationDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var app = await _db.Applications
            .Include(a => a.Parameters)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (app is null) return NotFound();
        return Ok(app.ToDetailDto());
    }

    [HttpPost]
    public async Task<ActionResult<ApplicationDetailDto>> Create(
        [FromBody] CreateApplicationRequest request,
        CancellationToken ct)
    {
        var nameTaken = await _db.Applications.AnyAsync(a => a.Name == request.Name, ct);
        if (nameTaken)
            return Conflict(new { error = $"An application named '{request.Name}' already exists." });

        var now = DateTimeOffset.UtcNow;
        var app = new ApplicationEntity
        {
            Id               = Guid.NewGuid(),
            Name             = request.Name,
            Description      = request.Description ?? string.Empty,
            ExecutablePath   = request.ExecutablePath,
            WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory) ? null : request.WorkingDirectory,
            TimeoutSeconds   = request.TimeoutSeconds,
            IsActive         = true,
            CreatedAt        = now,
            UpdatedAt        = now,
            Parameters       = (request.Parameters ?? Array.Empty<CreateParameterRequest>())
                .Select((p, i) => new ApplicationParameterEntity
                {
                    Id             = Guid.NewGuid(),
                    ParameterName  = p.ParameterName,
                    DisplayLabel   = p.DisplayLabel,
                    ParameterType  = p.ParameterType,
                    IsRequired     = p.IsRequired,
                    DefaultValue   = p.DefaultValue,
                    Description    = p.Description ?? string.Empty,
                    DisplayOrder   = p.DisplayOrder == 0 ? i : p.DisplayOrder,
                    CreatedAt      = now,
                    UpdatedAt      = now,
                })
                .ToList(),
        };

        _db.Applications.Add(app);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created application {AppName} ({AppId})", app.Name, app.Id);
        return CreatedAtAction(nameof(Get), new { id = app.Id }, app.ToDetailDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApplicationDetailDto>> Update(
        Guid id,
        [FromBody] UpdateApplicationRequest request,
        CancellationToken ct)
    {
        var app = await _db.Applications
            .Include(a => a.Parameters)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (app is null) return NotFound();

        if (app.Name != request.Name &&
            await _db.Applications.AnyAsync(a => a.Name == request.Name && a.Id != id, ct))
            return Conflict(new { error = $"An application named '{request.Name}' already exists." });

        app.Name             = request.Name;
        app.Description      = request.Description ?? string.Empty;
        app.ExecutablePath   = request.ExecutablePath;
        app.WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory) ? null : request.WorkingDirectory;
        app.TimeoutSeconds   = request.TimeoutSeconds;
        app.IsActive         = request.IsActive;

        await _db.SaveChangesAsync(ct);
        return Ok(app.ToDetailDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (app is null) return NotFound();

        // Soft delete: keep history, just deactivate. Hard delete would fail anyway
        // for apps with run_requests due to the ON DELETE RESTRICT FK.
        app.IsActive = false;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deactivated application {AppName} ({AppId})", app.Name, app.Id);
        return NoContent();
    }

    // ─────────── Parameters as a sub-resource ───────────
    [HttpPost("{appId:guid}/parameters")]
    public async Task<ActionResult<ParameterDto>> AddParameter(
        Guid appId,
        [FromBody] CreateParameterRequest request,
        CancellationToken ct)
    {
        var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == appId, ct);
        if (app is null) return NotFound();

        var duplicate = await _db.ApplicationParameters
            .AnyAsync(p => p.ApplicationId == appId && p.ParameterName == request.ParameterName, ct);
        if (duplicate)
            return Conflict(new { error = $"Parameter '{request.ParameterName}' already exists on this application." });

        var now = DateTimeOffset.UtcNow;
        var param = new ApplicationParameterEntity
        {
            Id            = Guid.NewGuid(),
            ApplicationId = appId,
            ParameterName = request.ParameterName,
            DisplayLabel  = request.DisplayLabel,
            ParameterType = request.ParameterType,
            IsRequired    = request.IsRequired,
            DefaultValue  = request.DefaultValue,
            Description   = request.Description ?? string.Empty,
            DisplayOrder  = request.DisplayOrder,
            CreatedAt     = now,
            UpdatedAt     = now,
        };
        _db.ApplicationParameters.Add(param);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(ParametersController.Get), "Parameters", new { id = param.Id }, param.ToDto());
    }
}
