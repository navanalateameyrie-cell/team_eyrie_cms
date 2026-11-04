using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>Practice-level checklist attached to one <see cref="ServiceDefinition"/>.</summary>
[Auditable]
public class ChecklistTemplate : Entity
{
    public Guid ServiceDefinitionId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public ServiceDefinition? ServiceDefinition { get; private set; }

    private ChecklistTemplate() { }

    public ChecklistTemplate(Guid serviceDefinitionId, string name)
    {
        if (serviceDefinitionId == Guid.Empty) throw new ArgumentException("ServiceDefinitionId required", nameof(serviceDefinitionId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name required", nameof(name));
        ServiceDefinitionId = serviceDefinitionId;
        Name = name.Trim();
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name required", nameof(name));
        Name = name.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

public class ChecklistTemplateItem : Entity
{
    public Guid TemplateId { get; private set; }
    public int Order { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool IsRequired { get; private set; }

    public ChecklistTemplate? Template { get; private set; }

    private ChecklistTemplateItem() { }

    public ChecklistTemplateItem(Guid templateId, int order, string text, bool isRequired)
    {
        if (templateId == Guid.Empty) throw new ArgumentException("TemplateId required", nameof(templateId));
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Text required", nameof(text));
        TemplateId = templateId;
        Order = order;
        Text = text.Trim();
        IsRequired = isRequired;
    }
}
