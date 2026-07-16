using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// Read-only access to a single record's 異動紀錄 (row-audit history). Cross-cutting: any detail/form
/// page passes its own <c>tableName</c> and the record's <c>pkid</c> to list that record's changes.
/// </summary>
[ApiController]
[Route("api/rowaudit")]
[Produces("application/json")]
public class RowAuditController(IRowAuditRepository repository) : ControllerBase
{
    /// <summary>
    /// One record's audit trail, newest first — e.g. <c>GET /api/rowaudit?tableName=Course&amp;pkid=123</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RowAuditEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RowAuditEntry>>> GetForRecord(
        [FromQuery] string? tableName, [FromQuery] int pkid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "缺少查詢條件",
                Detail = "請提供 tableName。"
            });
        }

        // PrimaryKeyValues is stored as the pkid's string form; compare on the string.
        var rows = await repository.GetForRecordAsync(tableName.Trim(), pkid.ToString(), ct);
        return Ok(rows);
    }
}
