using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// Historical staff assignment under a client or service file. Append-only except for the one
/// legal update: closing an open-ended row by stamping <see cref="EffectiveTo"/>. Reassignment is
/// a close + insert; historical rows are never rewritten in place.
/// </summary>
[Auditable]
public class StaffAssignment : Entity
{
    public AssignmentScope Scope { get; private set; }
    public Guid ScopeId { get; private set; }      // clients.Id or service_files.Id, per Scope
    public Guid StaffId { get; private set; }
    public AssignmentRole Role { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }

    public string? Notes { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; }

    public Staff? Staff { get; private set; }

    private StaffAssignment() { }

    public StaffAssignment(AssignmentScope scope, Guid scopeId, Guid staffId,
                           AssignmentRole role, DateOnly effectiveFrom, string? notes = null)
    {
        if (scopeId == Guid.Empty) throw new ArgumentException("ScopeId is required", nameof(scopeId));
        if (staffId == Guid.Empty) throw new ArgumentException("StaffId is required", nameof(staffId));

        Scope = scope;
        ScopeId = scopeId;
        StaffId = staffId;
        Role = role;
        EffectiveFrom = effectiveFrom;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Version = 1;
    }

    /// <summary>Close an open-ended assignment. The only mutation allowed on a historical row.</summary>
    public void End(DateOnly effectiveTo)
    {
        if (EffectiveTo is not null)
            throw new InvalidOperationException("Assignment is already closed.");
        if (effectiveTo < EffectiveFrom)
            throw new ArgumentException("EffectiveTo cannot precede EffectiveFrom.");
        EffectiveTo = effectiveTo;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }

    public bool IsActiveOn(DateOnly asOf) =>
        asOf >= EffectiveFrom && (EffectiveTo is null || asOf <= EffectiveTo);
}
