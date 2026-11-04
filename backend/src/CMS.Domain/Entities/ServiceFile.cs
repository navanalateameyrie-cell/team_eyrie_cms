using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// A file representing a single engagement of one service for one client. Independent lifecycle:
/// closing a file does not change the client or branch, and vice-versa. The 10-digit
/// <see cref="FileNumber"/> is permanent and preserved across close + reopen.
/// </summary>
[Auditable]
public class ServiceFile : Entity
{
    public string FileNumber { get; private set; } = string.Empty;

    /// <summary>Original identifier migrated from a legacy system; preserved separately from <see cref="FileNumber"/>.</summary>
    public string? LegacyFileNumber { get; private set; }

    public Guid ClientId { get; private set; }
    public Guid ServiceDefinitionId { get; private set; }
    public Guid? BranchId { get; private set; }

    public ServiceFileStatus Status { get; private set; } = ServiceFileStatus.Open;
    public DateOnly OpenedOn { get; private set; }
    public DateOnly? ClosedOn { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>Service-specific metadata JSON ({ field: value }). Validated against the parent ServiceDefinition's schema.</summary>
    public string? MetadataJson { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; }

    // Navigation (optional)
    public Client? Client { get; private set; }
    public ServiceDefinition? ServiceDefinition { get; private set; }
    public Branch? Branch { get; private set; }

    private ServiceFile() { }

    public ServiceFile(string fileNumber, Guid clientId, Guid serviceDefinitionId,
                       Guid? branchId, DateOnly openedOn,
                       string? legacyFileNumber = null, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(fileNumber)) throw new ArgumentException("File number is required", nameof(fileNumber));
        if (fileNumber.Length != 10 || !fileNumber.All(char.IsDigit))
            throw new ArgumentException("File number must be exactly 10 digits", nameof(fileNumber));
        if (clientId == Guid.Empty) throw new ArgumentException("ClientId is required", nameof(clientId));
        if (serviceDefinitionId == Guid.Empty) throw new ArgumentException("ServiceDefinitionId is required", nameof(serviceDefinitionId));

        FileNumber = fileNumber;
        ClientId = clientId;
        ServiceDefinitionId = serviceDefinitionId;
        BranchId = branchId;
        OpenedOn = openedOn;
        LegacyFileNumber = Clean(legacyFileNumber);
        Notes = Clean(notes);
        Status = ServiceFileStatus.Open;
        Version = 1;
    }

    public void UpdateProfile(Guid serviceDefinitionId, Guid? branchId,
                              string? legacyFileNumber, string? notes,
                              string? metadataJson = null)
    {
        if (serviceDefinitionId == Guid.Empty) throw new ArgumentException("ServiceDefinitionId is required", nameof(serviceDefinitionId));
        if (Status == ServiceFileStatus.Closed)
            throw new InvalidOperationException("Closed files cannot be edited; reopen first.");

        ServiceDefinitionId = serviceDefinitionId;
        BranchId = branchId;
        LegacyFileNumber = Clean(legacyFileNumber);
        Notes = Clean(notes);
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson;
        Touch();
    }

    public void SetMetadata(string? metadataJson)
    {
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson;
        Touch();
    }

    public void Close(DateOnly closedOn)
    {
        if (Status == ServiceFileStatus.Closed) return;
        Status = ServiceFileStatus.Closed;
        ClosedOn = closedOn;
        Touch();
    }

    public void Reopen()
    {
        if (Status != ServiceFileStatus.Closed) return;
        Status = ServiceFileStatus.Open;
        ClosedOn = null;
        Touch();
    }

    public void PutOnHold()
    {
        if (Status == ServiceFileStatus.Closed)
            throw new InvalidOperationException("Closed files cannot be put on hold; reopen first.");
        if (Status == ServiceFileStatus.OnHold) return;
        Status = ServiceFileStatus.OnHold;
        Touch();
    }

    public void Resume()
    {
        if (Status != ServiceFileStatus.OnHold) return;
        Status = ServiceFileStatus.Open;
        Touch();
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
