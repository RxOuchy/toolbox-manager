using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToolboxManager.Api.Data;
using ToolboxManager.Api.Dtos;

namespace ToolboxManager.Api.Controllers;

[ApiController]
[Route("api/parameters")]
public sealed class ParametersController : ControllerBase
{
    private readonly ToolboxDbContext _db;

    public ParametersController(ToolboxDbContext db)
    {
        _db = db;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ParameterDto>> Get(Guid id, CancellationToken ct)
    {
        var p = await _db.ApplicationParameters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return NotFound();
        return Ok(p.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ParameterDto>> Update(
        Guid id,
        [FromBody] UpdateParameterRequest request,
        CancellationToken ct)
    {
        var p = await _db.ApplicationParameters.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return NotFound();

        if (p.ParameterName != request.ParameterName &&
            await _db.ApplicationParameters.AnyAsync(x =>
                x.ApplicationId == p.ApplicationId &&
                x.ParameterName == request.ParameterName &&
                x.Id != id, ct))
        {
            return Conflict(new { error = $"Parameter '{request.ParameterName}' already exists on this application." });
        }

        p.ParameterName = request.ParameterName;
        p.DisplayLabel  = request.DisplayLabel;
        p.ParameterType = request.ParameterType;
        p.IsRequired    = request.IsRequired;
        p.DefaultValue  = request.DefaultValue;
        p.Description   = request.Description ?? string.Empty;
        p.DisplayOrder  = request.DisplayOrder;

        await _db.SaveChangesAsync(ct);
        return Ok(p.ToDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var p = await _db.ApplicationParameters.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return NotFound();
        _db.ApplicationParameters.Remove(p);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
