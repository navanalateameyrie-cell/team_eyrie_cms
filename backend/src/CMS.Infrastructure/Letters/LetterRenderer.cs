using System.Text.RegularExpressions;
using CMS.Application.Letters;
using CMS.Application.Settings;
using CMS.Infrastructure.Billing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CMS.Infrastructure.Letters;

/// <summary>
/// Mail-merge letter renderer. v1 is deliberately simple: <c>{{token}}</c> substitution with
/// paragraph-respecting plain-text layout. Unknown tokens pass through unchanged so authors can
/// see which placeholders need wiring. Firm letterhead comes from the live FirmProfile settings.
/// </summary>
public class LetterRenderer : ILetterRenderer
{
    private static readonly Regex Token = new(@"\{\{\s*([a-zA-Z0-9_.]+)\s*\}\}", RegexOptions.Compiled);

    static LetterRenderer() { QuestPDF.Settings.License = LicenseType.Community; }

    private readonly ISettingsStore _settings;
    public LetterRenderer(ISettingsStore settings) => _settings = settings;

    public string RenderText(string body, LetterContext context)
    {
        if (string.IsNullOrEmpty(body)) return body ?? string.Empty;
        return Token.Replace(body, m =>
        {
            var key = m.Groups[1].Value;
            return context.Tokens.TryGetValue(key, out var v) ? (v ?? string.Empty) : m.Value;
        });
    }

    public byte[] RenderPdf(string title, string body, LetterContext context)
    {
        var firm = _settings.GetAsync<FirmProfileOptions>(SettingKeys.FirmProfile).GetAwaiter().GetResult();
        var merged = RenderText(body, context);

        return QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(50);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(t => t.FontSize(11).FontColor(Colors.Grey.Darken4));

                page.Header().Column(h =>
                {
                    h.Item().Text(firm.Name).FontSize(14).Bold();
                    var address = string.Join(", ", new[] { firm.Line1, firm.Line2, firm.City, firm.State, firm.Pincode }
                        .Where(s => !string.IsNullOrWhiteSpace(s)));
                    if (!string.IsNullOrWhiteSpace(address))
                        h.Item().Text(address).FontSize(9).FontColor(Colors.Grey.Darken2);
                    if (!string.IsNullOrWhiteSpace(firm.Gstin))
                        h.Item().Text($"GSTIN: {firm.Gstin}    PAN: {firm.Pan}").FontSize(9).FontColor(Colors.Grey.Darken2);
                    h.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(14).Column(c =>
                {
                    if (!string.IsNullOrWhiteSpace(title))
                        c.Item().PaddingBottom(10).Text(title).FontSize(13).Bold();
                    foreach (var para in SplitParagraphs(merged))
                        c.Item().PaddingBottom(6).Text(para).LineHeight(1.4f);
                });

                page.Footer().AlignCenter().Text($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();
    }

    private static IEnumerable<string> SplitParagraphs(string text)
    {
        var normalised = text.Replace("\r\n", "\n");
        foreach (var block in normalised.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            yield return block.Trim();
    }
}
