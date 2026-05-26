using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToolboxManager.Api.Data;
using ToolboxManager.Api.Dtos;
using ToolboxManager.Api.Models;
using ToolboxManager.Api.Services;

namespace ToolboxManager.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class RunsController : ControllerBase
{
    private readonly ToolboxDbContext _db;
    private readonly IRunRequestService _runs;
    private readonly ILogger<RunsController> _logger;

    public RunsController(ToolboxDbContext db, IRunRequestService runs, ILogger<RunsController> logger)
    {
        _db = db;
        _runs = runs;
        _logger = logger;
    }

    /// <summary>Trigger a new run of an application.</summary>
    [HttpPost("applications/{appId:guid}/run")]
    public async Task<ActionResult<RunRequestDto>> Trigger(
        Guid appId,
        [FromBody] TriggerRunRequest request,
        CancellationToken ct)
    {
        try
        {
            var dto = await _runs.TriggerRunAsync(appId, request, ct);
            return Accepted(dto);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>List run requests, newest first.</summary>
    [HttpGet("runs")]
    public async Task<ActionResult<IReadOnlyList<RunRequestDto>>> List(
        [FromQuery] Guid? applicationId,
        [FromQuery] RunStatus? status,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (limit is < 1 or > 500)
            return BadRequest(new { error = "limit must be between 1 and 500." });

        var query = _db.RunRequests
            .Include(r => r.Application)
            .AsNoTracking()
            .OrderByDescending(r => r.QueuedAt)
            .AsQueryable();

        if (applicationId is { } appId)
            query = query.Where(r => r.ApplicationId == appId);

        if (status is { } s)
            query = query.Where(r => r.Status == s);

        var rows = await query.Take(limit).ToListAsync(ct);
        return Ok(rows.Select(r => r.ToDto()).ToList());
    }

    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult<RunRequestDto>> Get(Guid id, CancellationToken ct)
    {
        var run = await _db.RunRequests
            .Include(r => r.Application)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (run is null) return NotFound();
        return Ok(run.ToDto());
    }

    /// <summary>
    /// Status callback from the Polling Service. Reports start, stdout/stderr,
    /// exit code, and terminal status. Idempotent: re-applying the same terminal
    /// status is a no-op.
    /// </summary>
    [HttpPatch("runs/{id:guid}/status")]
    public async Task<ActionResult<RunRequestDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateRunStatusRequest request,
        CancellationToken ct)
    {
        var run = await _db.RunRequests
            .Include(r => r.Application)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null) return NotFound();

        if (run.Status.IsTerminal() && request.Status != run.Status)
        {
            _logger.LogWarning(
                "Refused to transition run {RunId} from terminal {Old} to {New}",
                id, run.Status, request.Status);
            return Conflict(new { error = $"Run is already in terminal state '{run.Status}'." });
        }

        run.Status        = request.Status;
        run.ExitCode      = request.ExitCode      ?? run.ExitCode;
        run.Stdout        = request.Stdout        ?? run.Stdout;
        run.Stderr        = request.Stderr        ?? run.Stderr;
        run.ErrorMessage  = request.ErrorMessage  ?? run.ErrorMessage;
        run.StartedAt     = request.StartedAt     ?? run.StartedAt;
        run.CompletedAt   = request.CompletedAt   ?? run.CompletedAt;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Run {RunId} status -> {Status} (exit={ExitCode})", id, run.Status, run.ExitCode);
        return Ok(run.ToDto());
    }
}
