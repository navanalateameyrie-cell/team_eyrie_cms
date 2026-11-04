namespace CMS.Domain.Entities;

/// <summary>Single-row counter per financial year for invoice sequence numbers.</summary>
public class InvoiceNumberCounter
{
    public string FinancialYear { get; private set; } = string.Empty; // "YYYY-YY"
    public long NextValue { get; private set; }

    private InvoiceNumberCounter() { }

    public InvoiceNumberCounter(string financialYear, long nextValue)
    {
        FinancialYear = financialYear;
        NextValue = nextValue;
    }
}
