using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// Reusable letter body with <c>{{placeholder}}</c> tokens. Rendered against a compliance
/// obligation context into a QuestPDF document. The practice edits templates centrally; existing
/// generated PDFs are not rewritten — each render reads the current body at the moment of use.
/// </summary>
[Auditable]
public class LetterTemplate : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    [ConcurrencyCheck]
    public int Version { get; private set; }

    private LetterTemplate() { }

    public LetterTemplate(string name, string body)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name required", nameof(name));
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("Body required", nameof(body));
        Name = name.Trim();
        Body = body;
        Version = 1;
    }

    public void Update(string name, string body)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name required", nameof(name));
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("Body required", nameof(body));
        Name = name.Trim();
        Body = body;
        Touch();
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        Touch();
    }

    private void Touch() { UpdatedAtUtc = DateTimeOffset.UtcNow; Version++; }
}
