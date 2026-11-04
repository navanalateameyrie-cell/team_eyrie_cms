using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// A client's registered presence in a state. One branch per state is the normal model.
/// Closing a branch is a branch-only state change — it never cascades to the client.
/// </summary>
[Auditable]
public class Branch : Entity
{
    public Guid ClientId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public BranchType Type { get; private set; } = BranchType.Branch;
    public Guid StateId { get; private set; }

    public Address Address { get; private set; } = default!;

    public bool IsPrimary { get; private set; }
    public bool IsBillingEnabled { get; private set; } = true;
    public bool IsActive { get; private set; } = true;

    public DateOnly OpenedOn { get; private set; }
    public DateOnly? ClosedOn { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; }

    public State? State { get; private set; }

    private Branch() { }

    public Branch(Guid clientId, string code, string name, BranchType type, Guid stateId,
                  Address address, DateOnly openedOn, bool isPrimary, bool isBillingEnabled)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("ClientId is required", nameof(clientId));
        if (stateId == Guid.Empty) throw new ArgumentException("StateId is required", nameof(stateId));
        SetCore(code, name, type);
        ClientId = clientId;
        StateId = stateId;
        Address = address ?? throw new ArgumentNullException(nameof(address));
        OpenedOn = openedOn;
        IsPrimary = isPrimary;
        IsBillingEnabled = isBillingEnabled;
        Version = 1;
    }

    public void UpdateProfile(string code, string name, BranchType type, Guid stateId, Address address, bool isBillingEnabled)
    {
        if (!IsActive) throw new InvalidOperationException("Closed branches cannot be edited; reopen first.");
        if (stateId == Guid.Empty) throw new ArgumentException("StateId is required", nameof(stateId));
        SetCore(code, name, type);
        StateId = stateId;
        Address = address ?? throw new ArgumentNullException(nameof(address));
        IsBillingEnabled = isBillingEnabled;
        Touch();
    }

    public void MarkPrimary() { IsPrimary = true; Touch(); }
    public void DemotePrimary() { IsPrimary = false; Touch(); }

    public void Close(DateOnly closedOn)
    {
        if (!IsActive) return;
        IsActive = false;
        ClosedOn = closedOn;
        Touch();
    }

    public void Reopen()
    {
        if (IsActive) return;
        IsActive = true;
        ClosedOn = null;
        Touch();
    }

    private void SetCore(string code, string name, BranchType type)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Code is required", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required", nameof(name));
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Type = type;
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }
}
