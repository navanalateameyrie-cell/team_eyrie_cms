namespace CMS.Domain.Entities;

/// <summary>Entity kinds that a <see cref="Document"/> may be attached to.</summary>
public enum DocumentOwnerType
{
    Client = 1,
    Branch = 2,
    ServiceFile = 3,
    ComplianceObligation = 4,
    Engagement = 5,
}
