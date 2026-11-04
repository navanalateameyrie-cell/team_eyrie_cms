namespace CMS.Domain.Entities;

public enum BankStatementLineStatus
{
    Unmatched = 1,
    MatchedInvoice = 2,
    MatchedAdvance = 3,
    Ignored = 4,
}
