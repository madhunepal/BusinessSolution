using SmallBusiness.Domain.Common;

namespace SmallBusiness.Domain.Entities;

/// <summary>
/// Security/administrative record of important changes.
/// Conceptually separate from Activity: AuditLog = accountability/security,
/// Activity = business timeline.
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>
    /// Business ID. Nullable for system-level events not tied to a tenant.
    /// </summary>
    public Guid? BusinessId { get; set; }

    /// <summary>
    /// What happened (e.g. "Create", "Update", "Delete", "Login").
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// The type of entity affected (e.g. "Customer", "Quote").
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// The ID of the affected entity.
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// JSON-serialized previous state (for updates/deletes).
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// JSON-serialized new state (for creates/updates).
    /// </summary>
    public string? NewValues { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Identity user ID who performed the action.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// IP address of the request, if available.
    /// </summary>
    public string? IpAddress { get; set; }
}
