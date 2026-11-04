namespace CMS.Domain.Entities;

public enum ImportBatchStatus
{
    Received = 1,
    Parsed = 2,
    Validated = 3,
    Committed = 4,
    Failed = 5,
    Cancelled = 6,
}
