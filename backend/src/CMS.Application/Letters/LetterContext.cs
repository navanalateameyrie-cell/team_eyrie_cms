namespace CMS.Application.Letters;

/// <summary>Flat token bag passed to the mail-merge resolver. Keys are case-insensitive.</summary>
public record LetterContext(IReadOnlyDictionary<string, string> Tokens);

public interface ILetterRenderer
{
    /// <summary>Substitute <c>{{token}}</c> occurrences against <paramref name="context"/>.</summary>
    string RenderText(string body, LetterContext context);

    /// <summary>Render the resolved body into a PDF with the firm letterhead.</summary>
    byte[] RenderPdf(string title, string body, LetterContext context);
}
