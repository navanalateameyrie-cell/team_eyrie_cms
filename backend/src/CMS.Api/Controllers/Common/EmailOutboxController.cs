using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/email-outbox")]
public class EmailOutboxController : ControllerBase
{
    private readonly CmsDbContext _db;
    public EmailOutboxController(CmsDbContext db) => _db = db;

    public record OutboxDto(
        Guid Id, DateTimeOffset CreatedAtUtc, EmailStatus Status, DateTimeOffset? SentAtUtc,
        string FromAddress, string? FromName,
        string ToAddresses, string? CcAddresses, string Subject,
        int AttachmentCount, long AttachmentTotalBytes,
        Guid? InvoiceId, string? SenderUserName, string? ErrorMessage);

    public record OutboxPage(IReadOnlyList<OutboxDto> Items, int Total, int Skip, int Take);

    [HttpGet]
    [HasPermission(Permissions.Email.Read)]
    public async Task<ActionResult<OutboxPage>> List(
        [FromQuery] Guid? invoiceId, [FromQuery] EmailStatus? status,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (take <= 0 || take > 500) take = 50;
        if (skip < 0) skip = 0;

        var q = _db.EmailOutbox.AsNoTracking().AsQueryable();
        if (invoiceId is not null) q = q.Where(e => e.InvoiceId == invoiceId);
        if (status is not null) q = q.Where(e => e.Status == status);

        var total = await q.CountAsync(ct);

        if (_db.Database.IsPostgres())
        {
            var pgRows = await q.OrderByDescending(e => e.CreatedAtUtc).Skip(skip).Take(take)
                .Select(e => new OutboxDto(
                    e.Id, e.CreatedAtUtc, e.Status, e.SentAtUtc,
                    e.FromAddress, e.FromName,
                    e.ToAddresses, e.CcAddresses, e.Subject,
                    e.AttachmentCount, e.AttachmentTotalBytes,
                    e.InvoiceId, e.SenderUserName, e.ErrorMessage)).ToListAsync(ct);
            return new OutboxPage(pgRows, total, skip, take);
        }

        // SQLite fallback — can't ORDER BY DateTimeOffset in SQL.
        var rows = await q.Select(e => new OutboxDto(
            e.Id, e.CreatedAtUtc, e.Status, e.SentAtUtc,
            e.FromAddress, e.FromName,
            e.ToAddresses, e.CcAddresses, e.Subject,
            e.AttachmentCount, e.AttachmentTotalBytes,
            e.InvoiceId, e.SenderUserName, e.ErrorMessage)).ToListAsync(ct);
        var ordered = rows.OrderByDescending(r => r.CreatedAtUtc).Skip(skip).Take(take).ToList();
        return new OutboxPage(ordered, total, skip, take);
    }
}
