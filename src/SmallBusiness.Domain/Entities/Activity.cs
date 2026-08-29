using SmallBusiness.Domain.Common;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Domain.Entities;

/// <summary>
/// Business timeline event. Records what happened, when, and to which entity.
/// Conceptually separate from AuditLog: Activity = business timeline,
/// AuditLog = accountability/security record.
/// </summary>
public class Activity : BusinessOwnedEntity
{
    public ActivityType ActivityType { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The type of entity this activity relates to (e.g. "Customer", "Quote").
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// The ID of the related entity.
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// The Identity user ID who performed this action.
    /// </summary>
    public string? CreatedBy { get; set; }

    // Navigation
    public Business Business { get; set; } = null!;
}
