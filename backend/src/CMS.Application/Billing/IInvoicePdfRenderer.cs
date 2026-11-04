namespace CMS.Application.Billing;

public interface IInvoicePdfRenderer
{
    /// <summary>Render the given invoice to PDF bytes.</summary>
    byte[] Render(InvoicePdfModel model);
}
