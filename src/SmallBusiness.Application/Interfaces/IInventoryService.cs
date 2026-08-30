using SmallBusiness.Application.DTOs.Inventory;

namespace SmallBusiness.Application.Interfaces;

public interface IInventoryService
{
    // Profiles
    Task<List<InventoryProfileDto>> GetInventoryProfilesAsync();
    Task<InventoryProfileDto> GetInventoryProfileAsync(Guid id);
    Task<InventoryProfileDto> CreateInventoryProfileAsync(CreateInventoryProfileDto request);
    Task<InventoryProfileDto> UpdateInventoryProfileAsync(UpdateInventoryProfileDto request);

    // Locations
    Task<List<InventoryLocationDto>> GetLocationsAsync();
    Task<InventoryLocationDto> CreateLocationAsync(CreateInventoryLocationDto request);

    // Ledger Operations
    Task<InventoryMovementDto> ReceiveStockAsync(StockReceiptDto request);
    Task<InventoryMovementDto> RecordUsageAsync(StockUsageDto request);
    Task<InventoryMovementDto> RecordWasteAsync(StockWasteDto request);
    Task<InventoryMovementDto> AdjustStockAsync(StockAdjustmentDto request);
    Task<List<InventoryMovementDto>> TransferStockAsync(StockTransferDto request);

    // Reporting
    Task<List<InventoryMovementDto>> GetMovementHistoryAsync(Guid profileId);
    Task<List<InventoryStockLevelDto>> GetStockLevelsAsync(Guid profileId);
    Task<List<InventoryProfileDto>> GetLowStockProfilesAsync();
    Task<List<InventoryLotDto>> GetExpiringLotsAsync(int daysToExpiration = 30);
}
