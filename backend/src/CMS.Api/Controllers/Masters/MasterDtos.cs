using System.ComponentModel.DataAnnotations;

namespace CMS.Api.Controllers.Masters;

public record MasterListQuery(string? Search, bool? Active);

public record EntityTypeDto(Guid Id, string Code, string Name, string? Description, int DisplayOrder, bool IsActive);
public record EntityTypeUpsertDto(
    [Required, StringLength(32)] string Code,
    [Required, StringLength(200)] string Name,
    [StringLength(1000)] string? Description,
    int DisplayOrder = 0);

public record StateDto(Guid Id, string Code, string Name, string CountryCode, int DisplayOrder, bool IsActive);
public record StateUpsertDto(
    [Required, StringLength(16)] string Code,
    [Required, StringLength(200)] string Name,
    [Required, StringLength(8)] string CountryCode = "IN",
    int DisplayOrder = 0);

public record ServiceDto(Guid Id, string Code, string Name, string? Category, string? Description,
                         int DisplayOrder, bool IsActive,
                         string? RequiredFieldsJson, string? OptionalFieldsJson,
                         CMS.Domain.Entities.ComplianceFrequency? Frequency, int? DueDayOffsetDays);
public record ServiceUpsertDto(
    [Required, StringLength(32)] string Code,
    [Required, StringLength(200)] string Name,
    [StringLength(100)] string? Category,
    [StringLength(1000)] string? Description,
    int DisplayOrder = 0,
    string? RequiredFieldsJson = null,
    string? OptionalFieldsJson = null,
    CMS.Domain.Entities.ComplianceFrequency? Frequency = null,
    int? DueDayOffsetDays = null);

public record StaffDto(Guid Id, string Code, string FullName, string Email, string? Phone, string? Role, bool IsActive);
public record StaffUpsertDto(
    [Required, StringLength(200)] string FullName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [StringLength(32)] string? Code,
    [StringLength(32)] string? Phone,
    [StringLength(100)] string? Role);

public record ClientGroupDto(Guid Id, string Code, string Name, string? Description, bool IsActive);
public record ClientGroupUpsertDto(
    [Required, StringLength(32)] string Code,
    [Required, StringLength(200)] string Name,
    [StringLength(1000)] string? Description);
