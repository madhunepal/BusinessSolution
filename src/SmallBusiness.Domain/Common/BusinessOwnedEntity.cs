namespace SmallBusiness.Domain.Common;

/// <summary>
/// Base class for entities that belong to a specific business (tenant).
/// Combines identity, timestamps, and tenant ownership.
/// </summary>
public abstract class BusinessOwnedEntity : BaseEntity, IHasBusinessId
{
    public Guid BusinessId { get; set; }
}
