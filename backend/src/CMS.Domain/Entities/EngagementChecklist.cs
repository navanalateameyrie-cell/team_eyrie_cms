using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>Per-engagement instance of a <see cref="ChecklistTemplate"/>.</summary>
[Auditable]
public class EngagementChecklist : Entity
{
    public Guid EngagementId { get; private set; }
    public Guid TemplateId { get; private set; }

    public Engagement? Engagement { get; private set; }
    public ChecklistTemplate? Template { get; private set; }

    private EngagementChecklist() { }

    public EngagementChecklist(Guid engagementId, Guid templateId)
    {
        if (engagementId == Guid.Empty) throw new ArgumentException("EngagementId required", nameof(engagementId));
        if (templateId == Guid.Empty) throw new ArgumentException("TemplateId required", nameof(templateId));
        EngagementId = engagementId;
        TemplateId = templateId;
    }
}

public class EngagementChecklistItem : Entity
{
    public Guid ChecklistId { get; private set; }
    public Guid TemplateItemId { get; private set; }

    /// <summary>Captured at the moment of instantiation so renaming a template doesn't rewrite history.</summary>
    public string Text { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public bool IsRequired { get; private set; }

    public bool IsCompleted { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public Guid? CompletedByStaffId { get; private set; }
    public string? Note { get; private set; }

    public EngagementChecklist? Checklist { get; private set; }

    private EngagementChecklistItem() { }

    public EngagementChecklistItem(Guid checklistId, Guid templateItemId, string text, int order, bool isRequired)
    {
        if (checklistId == Guid.Empty) throw new ArgumentException("ChecklistId required", nameof(checklistId));
        if (templateItemId == Guid.Empty) throw new ArgumentException("TemplateItemId required", nameof(templateItemId));
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Text required", nameof(text));
        ChecklistId = checklistId;
        TemplateItemId = templateItemId;
        Text = text.Trim();
        Order = order;
        IsRequired = isRequired;
    }

    public void Toggle(Guid staffId, string? note)
    {
        IsCompleted = !IsCompleted;
        if (IsCompleted)
        {
            CompletedAtUtc = DateTimeOffset.UtcNow;
            CompletedByStaffId = staffId == Guid.Empty ? null : staffId;
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        }
        else
        {
            CompletedAtUtc = null;
            CompletedByStaffId = null;
            Note = null;
        }
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
