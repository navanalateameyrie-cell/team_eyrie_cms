namespace CMS.Application.Abstractions;

public interface IInvoiceNumberGenerator
{
    /// <summary>Reserve and return a new invoice number for the given FY (format <c>YYYY-YY</c>, e.g. "2024-25").</summary>
    Task<string> NextAsync(string financialYear, CancellationToken ct = default);

    /// <summary>Reserve a document number for a non-invoice kind (CRN / DBN). Independent FY sequence per kind.</summary>
    Task<string> NextForKindAsync(string kind, string financialYear, CancellationToken ct = default);
}
