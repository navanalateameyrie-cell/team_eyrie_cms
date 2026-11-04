using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>Catalog row for a bank-statement CSV upload. Holds the counters surfaced in the reconcile UI.</summary>
[Auditable]
public class BankStatementBatch : Entity
{
    public string FileName { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }

    public int LineCount { get; private set; }
    public int MatchedInvoiceCount { get; private set; }
    public int MatchedAdvanceCount { get; private set; }
    public int IgnoredCount { get; private set; }

    public Guid? UploadedByUserId { get; private set; }
    public string? UploadedByUserName { get; private set; }

    private BankStatementBatch() { }

    public BankStatementBatch(string fileName, long sizeBytes, Guid? userId, string? userName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("FileName required", nameof(fileName));
        if (sizeBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        FileName = fileName.Trim();
        SizeBytes = sizeBytes;
        UploadedByUserId = userId;
        UploadedByUserName = string.IsNullOrWhiteSpace(userName) ? null : userName!.Trim();
    }

    public void RollupCounts(int lineCount, int matchedInvoice, int matchedAdvance, int ignored)
    {
        LineCount = lineCount;
        MatchedInvoiceCount = matchedInvoice;
        MatchedAdvanceCount = matchedAdvance;
        IgnoredCount = ignored;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
