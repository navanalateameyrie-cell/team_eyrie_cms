using Asp.Versioning;
using CMS.Application.Abstractions;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly ICurrentUser _me;
    public NotificationsController(CmsDbContext db, ICurrentUser me) { _db = db; _me = me; }

    public record NotificationDto(Guid Id, string Kind, string Title, string Body, string? Link,
                                   DateTimeOffset CreatedAtUtc, DateTimeOffset? ReadAtUtc);

    public record NotificationsListResponse(int UnreadCount, IReadOnlyList<NotificationDto> Items);

    [HttpGet]
    public async Task<ActionResult<NotificationsListResponse>> List(
        [FromQuery] bool onlyUnread = false, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        if (_me.UserId is null) return Unauthorized();
        if (take <= 0 || take > 500) take = 50;

        var q = _db.Notifications.AsNoTracking().Where(n => n.UserId == _me.UserId.Value);
        if (onlyUnread) q = q.Where(n => n.ReadAtUtc == null);

        List<NotificationDto> ordered;
        if (_db.Database.IsPostgres())
        {
            ordered = await q.OrderByDescending(n => n.CreatedAtUtc).Take(take)
                .Select(n => new NotificationDto(
                    n.Id, n.Kind, n.Title, n.Body, n.Link, n.CreatedAtUtc, n.ReadAtUtc))
                .ToListAsync(ct);
        }
        else
        {
            // SQLite fallback — can't ORDER BY DateTimeOffset in SQL.
            var rows = await q.Select(n => new NotificationDto(
                n.Id, n.Kind, n.Title, n.Body, n.Link, n.CreatedAtUtc, n.ReadAtUtc)).ToListAsync(ct);
            ordered = rows.OrderByDescending(r => r.CreatedAtUtc).Take(take).ToList();
        }

        var unread = await _db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == _me.UserId.Value && n.ReadAtUtc == null, ct);
        return new NotificationsListResponse(unread, ordered);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        if (_me.UserId is null) return Unauthorized();
        var row = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == _me.UserId, ct);
        if (row is null) return NotFound();
        row.MarkRead();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        if (_me.UserId is null) return Unauthorized();
        var unread = await _db.Notifications
            .Where(n => n.UserId == _me.UserId.Value && n.ReadAtUtc == null)
            .ToListAsync(ct);
        foreach (var n in unread) n.MarkRead();
        await _db.SaveChangesAsync(ct);
        return Ok(new { marked = unread.Count });
    }
}
