using SmallBusiness.Domain.Common;

namespace SmallBusiness.Domain.Entities;

public class InventoryStockLevel : BusinessOwnedEntity
{
    public Guid InventoryProfileId { get; set; }
    public Guid InventoryLocationId { get; set; }
    public Guid? InventoryLotId { get; set; }

    public decimal QuantityOnHand { get; set; }
    
    public byte[] RowVersion { get; set; } = null!;

    // Navigation
    public InventoryProfile InventoryProfile { get; set; } = null!;
    public InventoryLocation InventoryLocation { get; set; } = null!;
    public InventoryLot? InventoryLot { get; set; }
}
