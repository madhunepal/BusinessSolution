namespace SmallBusiness.Application.DTOs.Inventory;

public class InventoryLotDto
{
    public Guid Id { get; set; }
    public Guid InventoryProfileId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public DateOnly? ReceivedDate { get; set; }
    public DateOnly? ProductionDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Notes { get; set; }
}

public class CreateInventoryLotDto
{
    public string LotNumber { get; set; } = string.Empty;
    public DateOnly? ReceivedDate { get; set; }
    public DateOnly? ProductionDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Notes { get; set; }
}
