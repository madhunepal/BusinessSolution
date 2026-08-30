using SmallBusiness.Domain.Common;

namespace SmallBusiness.Domain.Entities;

public class InventoryLot : BusinessOwnedEntity
{
    public Guid InventoryProfileId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    
    public DateOnly? ReceivedDate { get; set; }
    public DateOnly? ProductionDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    
    public decimal? UnitCost { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public InventoryProfile InventoryProfile { get; set; } = null!;
}
