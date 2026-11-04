namespace CMS.Domain.Entities;

/// <summary>FY-scoped sequence counter per document kind (CRN / DBN). Composite PK (Kind, FY).</summary>
public class DocumentNumberCounter
{
    public string Kind { get; private set; } = string.Empty;
    public string FinancialYear { get; private set; } = string.Empty;
    public long NextValue { get; private set; }

    private DocumentNumberCounter() { }

    public DocumentNumberCounter(string kind, string financialYear, long nextValue)
    {
        Kind = kind;
        FinancialYear = financialYear;
        NextValue = nextValue;
    }
}
