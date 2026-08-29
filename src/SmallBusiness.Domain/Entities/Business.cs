using SmallBusiness.Domain.Common;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Domain.Entities;

/// <summary>
/// Represents a tenant/organization. Top-level entity for multi-tenancy.
/// All business-owned data is scoped to a Business.
/// </summary>
public class Business : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "UTC";
    public BusinessStatus Status { get; set; } = BusinessStatus.Active;

    // Contact/profile
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }

    // Address
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    // Tax / registration
    public string? TaxId { get; set; }
    public string? BusinessRegistrationNumber { get; set; }

    // Navigation
    public ICollection<BusinessUser> BusinessUsers { get; set; } = [];
    public ICollection<Activity> Activities { get; set; } = [];
}
