namespace SmallBusiness.Domain.Common;

/// <summary>
/// Interface for entities that belong to a specific business (tenant).
/// Used for global query filter tenant isolation.
/// </summary>
public interface IHasBusinessId
{
    Guid BusinessId { get; set; }
}
