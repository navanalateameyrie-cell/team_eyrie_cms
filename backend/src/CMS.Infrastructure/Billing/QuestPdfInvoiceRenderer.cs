using CMS.Application.Billing;
using CMS.Application.Settings;
using CMS.Domain.Entities;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CMS.Infrastructure.Billing;

/// <summary>
/// Renders a GST-compliant tax invoice PDF using QuestPDF's fluent layout. Keeps a single-page
/// layout at common line counts; QuestPDF handles overflow pagination automatically.
/// </summary>
public class QuestPdfInvoiceRenderer : IInvoicePdfRenderer
{
    private readonly ISettingsStore _settings;
    private FirmProfileOptions _firm = new();

    static QuestPdfInvoiceRenderer()
    {
        // Community license is enough for an internal CA practice tool; set once per process.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public QuestPdfInvoiceRenderer(ISettingsStore settings) => _settings = settings;

    public byte[] Render(InvoicePdfModel m)
    {
        // Resolve the current firm profile synchronously — Render is called from the request path
        // and the store already caches per-scope.
        _firm = _settings.GetAsync<FirmProfileOptions>(SettingKeys.FirmProfile).GetAwaiter().GetResult();

        return QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Column(h =>
                {
                    h.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(_firm.Name).FontSize(16).Bold().FontColor(Colors.Grey.Darken4);
                            c.Item().Text(ComposeFirmAddress()).FontSize(9).FontColor(Colors.Grey.Darken2);
                            if (!string.IsNullOrWhiteSpace(_firm.Gstin))
                                c.Item().Text($"GSTIN: {_firm.Gstin}    PAN: {_firm.Pan}").FontSize(9);
                            var contact = string.Join("  ·  ",
                                new[] { _firm.Phone, _firm.Email, _firm.Website }.Where(s => !string.IsNullOrWhiteSpace(s)));
                            if (!string.IsNullOrWhiteSpace(contact))
                                c.Item().Text(contact).FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(170).AlignRight().Column(c =>
                        {
                            c.Item().Text("TAX INVOICE").FontSize(18).Bold().FontColor(Colors.Orange.Darken2);
                            c.Item().Text($"# {m.Number}").FontSize(10).Bold();
                            c.Item().Text($"FY {m.FinancialYear}").FontSize(9).FontColor(Colors.Grey.Darken2);
                            c.Item().PaddingTop(4).Text($"Status: {m.Status}")
                                .FontSize(9).FontColor(StatusColor(m.Status));
                        });
                    });
                    h.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    // Bill-to + meta grid
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("Bill to").FontSize(8).FontColor(Colors.Grey.Darken2).Bold();
                            c.Item().PaddingTop(2).Text(m.ClientName).Bold();
                            if (!string.IsNullOrWhiteSpace(m.ClientAddress))
                                c.Item().Text(m.ClientAddress).FontSize(9);
                            if (!string.IsNullOrWhiteSpace(m.ClientGstin))
                                c.Item().Text($"GSTIN: {m.ClientGstin}").FontSize(9);
                            if (!string.IsNullOrWhiteSpace(m.ClientPan))
                                c.Item().Text($"PAN: {m.ClientPan}").FontSize(9);
                        });

                        row.ConstantItem(10);

                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            MetaRow(c, "Issue date", m.IssueDate.ToString("dd MMM yyyy"));
                            MetaRow(c, "Due date", m.DueDate.ToString("dd MMM yyyy"));
                            MetaRow(c, "Place of supply", m.PlaceOfSupplyState);
                            MetaRow(c, "Tax type", m.IsIntraState ? "CGST + SGST (intra-state)" : "IGST (inter-state)");
                        });
                    });

                    col.Item().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(22);  // #
                            cols.RelativeColumn(3);   // description
                            cols.ConstantColumn(50);  // service
                            cols.ConstantColumn(44);  // qty
                            cols.ConstantColumn(70);  // unit
                            cols.ConstantColumn(42);  // gst%
                            cols.ConstantColumn(80);  // total
                        });

                        table.Header(h =>
                        {
                            HeaderCell(h.Cell(), "#", right: true);
                            HeaderCell(h.Cell(), "Description");
                            HeaderCell(h.Cell(), "Service");
                            HeaderCell(h.Cell(), "Qty", right: true);
                            HeaderCell(h.Cell(), "Unit (₹)", right: true);
                            HeaderCell(h.Cell(), "GST %", right: true);
                            HeaderCell(h.Cell(), "Line (₹)", right: true);
                        });

                        foreach (var line in m.Lines)
                        {
                            BodyCell(table.Cell(), line.LineNumber.ToString(), right: true);
                            BodyCell(table.Cell(), DescriptionBlock(line));
                            BodyCell(table.Cell(), line.ServiceCode ?? "—");
                            BodyCell(table.Cell(), line.Quantity.ToString("0.###"), right: true);
                            BodyCell(table.Cell(), line.UnitPriceInr.ToString("N2"), right: true);
                            BodyCell(table.Cell(), line.GstRatePercent.ToString("0.##"), right: true);
                            BodyCell(table.Cell(), line.LineTotalInr.ToString("N2"), right: true);
                        }
                    });

                    // Totals block
                    col.Item().AlignRight().Width(260).Column(c =>
                    {
                        TotalRow(c, "Subtotal", m.SubtotalInr);
                        if (m.IsIntraState)
                        {
                            TotalRow(c, "CGST", m.CgstInr);
                            TotalRow(c, "SGST", m.SgstInr);
                        }
                        else
                        {
                            TotalRow(c, "IGST", m.IgstInr);
                        }
                        c.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        TotalRow(c, "Grand total", m.TotalInr, bold: true);
                        if (m.PaidInr > 0)
                        {
                            TotalRow(c, "Paid", m.PaidInr, color: Colors.Green.Darken2);
                            TotalRow(c, "Outstanding", m.OutstandingInr, bold: true, color: Colors.Red.Darken2);
                        }
                    });

                    col.Item().PaddingTop(8).Text(txt =>
                    {
                        txt.Span("Amount in words: ").SemiBold();
                        txt.Span(AmountInWords(m.TotalInr) + " only").Italic();
                    });

                    // IRN + QR block — only when the IRP has issued one.
                    if (!string.IsNullOrWhiteSpace(m.Irn) && !string.IsNullOrWhiteSpace(m.QrText))
                    {
                        var qr = RenderQrPng(m.QrText!);
                        col.Item().PaddingTop(10).Row(row =>
                        {
                            row.ConstantItem(110).Image(qr);
                            row.ConstantItem(12);
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("E-invoice (IRN)").FontSize(8).Bold().FontColor(Colors.Grey.Darken2);
                                c.Item().Text($"IRN: {m.Irn}").FontSize(8).FontColor(Colors.Grey.Darken3);
                                if (!string.IsNullOrWhiteSpace(m.AckNo))
                                    c.Item().Text($"Ack No: {m.AckNo}").FontSize(8);
                                if (m.AckDateUtc.HasValue)
                                    c.Item().Text($"Ack Date: {m.AckDateUtc.Value:dd MMM yyyy HH:mm} UTC").FontSize(8);
                            });
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(m.Notes))
                    {
                        col.Item().PaddingTop(8).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(c =>
                        {
                            c.Item().Text("Notes").FontSize(8).Bold().FontColor(Colors.Grey.Darken2);
                            c.Item().Text(m.Notes!).FontSize(9);
                        });
                    }

                    if (HasBankDetails())
                    {
                        col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(c =>
                        {
                            c.Item().Text("Payment details").FontSize(8).Bold().FontColor(Colors.Grey.Darken2);
                            var b = _firm.Bank;
                            if (!string.IsNullOrWhiteSpace(b.AccountName)) c.Item().Text($"A/c name: {b.AccountName}").FontSize(9);
                            if (!string.IsNullOrWhiteSpace(b.AccountNumber)) c.Item().Text($"A/c number: {b.AccountNumber}").FontSize(9);
                            if (!string.IsNullOrWhiteSpace(b.Ifsc)) c.Item().Text($"IFSC: {b.Ifsc}").FontSize(9);
                            if (!string.IsNullOrWhiteSpace(b.BankName)) c.Item().Text($"Bank: {b.BankName} ({b.BranchName})").FontSize(9);
                        });
                    }
                });

                page.Footer().Column(f =>
                {
                    if (!string.IsNullOrWhiteSpace(m.SignerName))
                    {
                        f.Item().PaddingBottom(4).Border(1).BorderColor(Colors.Blue.Lighten2).Background(Colors.Blue.Lighten5).Padding(6).Row(row =>
                        {
                            row.ConstantItem(14).AlignTop().Text("✓").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(t =>
                                {
                                    t.Span("Digitally signed by ").FontSize(8).FontColor(Colors.Blue.Darken3);
                                    t.Span(m.SignerName!).FontSize(8).Bold().FontColor(Colors.Blue.Darken4);
                                });
                                var thumb = (m.SignatureThumbprint ?? "").Trim();
                                var tail = thumb.Length > 12 ? thumb[^12..] : thumb;
                                c.Item().Text($"Thumbprint: …{tail}    Signed: {m.SignedAtUtc:yyyy-MM-dd HH:mm} UTC")
                                    .FontSize(7).FontColor(Colors.Blue.Darken1);
                            });
                        });
                    }
                    f.Item().AlignCenter().Text(t =>
                    {
                        t.Span("This is a system-generated tax invoice. ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        t.Span($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            });
        }).GeneratePdf();
    }

    private static string DescriptionBlock(InvoicePdfLine line)
    {
        var parts = new List<string> { line.Description };
        if (!string.IsNullOrWhiteSpace(line.FileNumber)) parts.Add($"File #{line.FileNumber}");
        if (!string.IsNullOrWhiteSpace(line.ObligationPeriodKey)) parts.Add($"Period {line.ObligationPeriodKey}");
        return string.Join("  ·  ", parts);
    }

    private static void HeaderCell(IContainer cell, string text, bool right = false)
    {
        var container = cell.Background(Colors.Grey.Lighten3).PaddingVertical(4).PaddingHorizontal(4);
        var aligned = right ? container.AlignRight() : container.AlignLeft();
        aligned.Text(text).FontSize(9).Bold();
    }

    private static void BodyCell(IContainer cell, string text, bool right = false)
    {
        var container = cell.PaddingVertical(3).PaddingHorizontal(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);
        var aligned = right ? container.AlignRight() : container.AlignLeft();
        aligned.Text(text).FontSize(9);
    }

    private static void MetaRow(ColumnDescriptor c, string label, string value) =>
        c.Item().Row(r =>
        {
            r.ConstantItem(80).Text(label).FontSize(8).FontColor(Colors.Grey.Darken2);
            r.RelativeItem().Text(value).FontSize(9);
        });

    private static void TotalRow(ColumnDescriptor c, string label, decimal amount,
                                  bool bold = false, string? color = null)
    {
        c.Item().Row(r =>
        {
            var l = r.RelativeItem().Text(label).FontSize(9);
            var v = r.ConstantItem(90).AlignRight().Text($"₹ {amount:N2}").FontSize(9);
            if (bold) { l.Bold(); v.Bold(); }
            if (color is not null) { l.FontColor(color); v.FontColor(color); }
        });
    }

    private static string StatusColor(InvoiceStatus s) => s switch
    {
        InvoiceStatus.Paid => Colors.Green.Darken2,
        InvoiceStatus.Cancelled => Colors.Red.Darken2,
        InvoiceStatus.PartPaid => Colors.Orange.Darken2,
        InvoiceStatus.Issued => Colors.Blue.Darken2,
        _ => Colors.Grey.Darken2,
    };

    private string ComposeFirmAddress()
    {
        var parts = new[] { _firm.Line1, _firm.Line2, _firm.City, _firm.State, _firm.Pincode }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join(", ", parts);
    }

    private bool HasBankDetails() =>
        !string.IsNullOrWhiteSpace(_firm.Bank.AccountNumber) ||
        !string.IsNullOrWhiteSpace(_firm.Bank.Ifsc) ||
        !string.IsNullOrWhiteSpace(_firm.Bank.BankName);

    /// <summary>Render an INR amount as English words (used in the "Amount in words" line).</summary>
    public static string AmountInWords(decimal amount)
    {
        var rupees = (long)Math.Floor(amount);
        var paise = (long)Math.Round((amount - rupees) * 100m);
        var sb = new System.Text.StringBuilder();
        sb.Append("Rupees ").Append(IndianNumberToWords(rupees));
        if (paise > 0) sb.Append(" and ").Append(IndianNumberToWords(paise)).Append(" paise");
        return sb.ToString();
    }

    private static byte[] RenderQrPng(string payload)
    {
        using var qrGen = new QRCodeGenerator();
        using var data = qrGen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        // PngByteQRCode is pure-managed (no SkiaSharp) and tiny — exactly what we want for a PDF embed.
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule: 6);
    }

    private static string IndianNumberToWords(long n)
    {
        if (n == 0) return "zero";
        var sb = new System.Text.StringBuilder();
        void Append(long value, string unit)
        {
            if (value == 0) return;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(TwoDigits(value)).Append(' ').Append(unit);
        }
        Append(n / 10000000, "crore"); n %= 10000000;
        Append(n / 100000, "lakh");    n %= 100000;
        Append(n / 1000, "thousand");  n %= 1000;
        Append(n / 100, "hundred");    n %= 100;
        if (n > 0)
        {
            if (sb.Length > 0) sb.Append(" and ");
            sb.Append(TwoDigits(n));
        }
        return sb.ToString();
    }

    private static string TwoDigits(long n)
    {
        string[] units = { "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
            "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
        string[] tens = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };
        if (n < 20) return units[n];
        var t = tens[n / 10];
        var u = n % 10;
        return u == 0 ? t : $"{t}-{units[u]}";
    }
}
