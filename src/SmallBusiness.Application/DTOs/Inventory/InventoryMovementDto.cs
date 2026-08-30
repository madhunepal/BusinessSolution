using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.DTOs.Inventory;

public class InventoryMovementDto
{
    public Guid Id { get; set; }
    public Guid InventoryProfileId { get; set; }
    public Guid InventoryLocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    
    public Guid? InventoryLotId { get; set; }
    public string? LotNumber { get; set; }

    public InventoryMovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }

    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }

    public string? Reason { get; set; }
    public string? Notes { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class InventoryStockLevelDto
{
    public Guid InventoryProfileId { get; set; }
    public Guid InventoryLocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public Guid? InventoryLotId { get; set; }
    public string? LotNumber { get; set; }
    
    public decimal QuantityOnHand { get; set; }
}

public class StockReceiptDto
{
    public Guid InventoryProfileId { get; set; }
    public Guid InventoryLocationId { get; set; }
    
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    
    public string? Notes { get; set; }
    
    // For lots
    public Guid? ExistingLotId { get; set; }
    public CreateInventoryLotDto? NewLot { get; set; }
}

public class StockUsageDto
{
    public Guid InventoryProfileId { get; set; }
    public Guid InventoryLocationId { get; set; }
    public Guid? InventoryLotId { get; set; }
    
    public decimal Quantity { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}

public class StockWasteDto
{
    public Guid InventoryProfileId { get; set; }
    public Guid InventoryLocationId { get; set; }
    public Guid? InventoryLotId { get; set; }
    
    public decimal Quantity { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}

public class StockAdjustmentDto
{
    public Guid InventoryProfileId { get; set; }
    public Guid InventoryLocationId { get; set; }
    public Guid? InventoryLotId { get; set; }
    
    public decimal QuantityDifference { get; set; } // positive or negative
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}

public class StockTransferDto
{
    public Guid InventoryProfileId { get; set; }
    public Guid SourceLocationId { get; set; }
    public Guid DestinationLocationId { get; set; }
    public Guid? InventoryLotId { get; set; }
    
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}
