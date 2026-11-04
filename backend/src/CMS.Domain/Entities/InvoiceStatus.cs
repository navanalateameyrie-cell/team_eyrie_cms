namespace CMS.Domain.Entities;

public enum InvoiceStatus
{
    Draft = 1,
    Issued = 2,
    PartPaid = 3,
    Paid = 4,
    Cancelled = 5,
}
