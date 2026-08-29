namespace SmallBusiness.Domain.Entities;

public class TenantSequence
{
    public Guid BusinessId { get; set; }
    
    public string EntityType { get; set; } = string.Empty;
    
    public int CurrentValue { get; set; }
    
    // Concurrency token to prevent dirty reads/updates
    public byte[] RowVersion { get; set; } = null!;
}
