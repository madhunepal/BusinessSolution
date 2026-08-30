using SmallBusiness.Domain.Common;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Domain.Entities;

public class InventoryMovement : BusinessOwnedEntity
{
    public Guid InventoryProfileId { get; set; }
    public Guid InventoryLocationId { get; set; }
    public Guid? InventoryLotId { get; set; }

    public InventoryMovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }

    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }

    public string? Reason { get; set; }
    public string? Notes { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;

    // Navigation
    public InventoryProfile InventoryProfile { get; set; } = null!;
    public InventoryLocation InventoryLocation { get; set; } = null!;
    public InventoryLot? InventoryLot { get; set; }
}
