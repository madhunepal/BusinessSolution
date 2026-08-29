namespace SmallBusiness.Domain.Common;

/// <summary>
/// Base class for all entities. Provides identity and auditing timestamps.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
