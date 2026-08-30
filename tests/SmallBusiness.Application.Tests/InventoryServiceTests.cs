using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using SmallBusiness.Application.DTOs.Inventory;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Application.Services;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;
using SmallBusiness.Infrastructure.Data;
using Xunit;

namespace SmallBusiness.Application.Tests;

public class InventoryServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly InventoryService _service;
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly string _userId = "test-user-id";

    public InventoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(x => x.CurrentBusinessId).Returns(_businessId);
        _mockTenantContext.Setup(x => x.UserId).Returns(_userId);

        _context = new ApplicationDbContext(options, _mockTenantContext.Object);
        
        _service = new InventoryService(_context, _mockTenantContext.Object);
    }

    [Fact]
    public async Task CreateInventoryProfile_Product_Succeeds()
    {
        var product = new CatalogItem { Id = Guid.NewGuid(), BusinessId = _businessId, Type = CatalogItemType.Product, Name = "Prod" };
        _context.CatalogItems.Add(product);
        await _context.SaveChangesAsync();

        var req = new CreateInventoryProfileDto { CatalogItemId = product.Id, ReorderLevel = 5 };
        var profile = await _service.CreateInventoryProfileAsync(req);

        Assert.NotNull(profile);
        Assert.Equal(product.Id, profile.CatalogItemId);
        
        var act = await _context.Activities.FirstOrDefaultAsync(a => a.EntityId == profile.Id);
        Assert.NotNull(act);
        Assert.Equal(ActivityType.Created, act.ActivityType);
    }

    [Fact]
    public async Task CreateInventoryProfile_Service_ThrowsValidationException()
    {
        var serviceItem = new CatalogItem { Id = Guid.NewGuid(), BusinessId = _businessId, Type = CatalogItemType.Service, Name = "Svc" };
        _context.CatalogItems.Add(serviceItem);
        await _context.SaveChangesAsync();

        var req = new CreateInventoryProfileDto { CatalogItemId = serviceItem.Id, ReorderLevel = 5 };
        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateInventoryProfileAsync(req));
    }

    [Fact]
    public async Task ReceiveStock_IncreasesStockAndGeneratesActivity()
    {
        var (profile, loc) = await SetupProfileAndLocationAsync();
        
        var req = new StockReceiptDto { InventoryProfileId = profile.Id, InventoryLocationId = loc.Id, Quantity = 10 };
        var mov = await _service.ReceiveStockAsync(req);

        Assert.Equal(10, mov.Quantity);
        
        var bucket = await _context.InventoryStockLevels.FirstAsync();
        Assert.Equal(10, bucket.QuantityOnHand);

        var act = await _context.Activities.FirstOrDefaultAsync(a => a.ActivityType == ActivityType.StockReceived);
        Assert.NotNull(act);
    }

    [Fact]
    public async Task Usage_DecreasesStock()
    {
        var (profile, loc) = await SetupProfileAndLocationAsync(allowNegative: true);
        
        var req = new StockUsageDto { InventoryProfileId = profile.Id, InventoryLocationId = loc.Id, Quantity = 3 };
        var mov = await _service.RecordUsageAsync(req);

        Assert.Equal(-3, mov.Quantity);
        
        var bucket = await _context.InventoryStockLevels.FirstAsync();
        Assert.Equal(-3, bucket.QuantityOnHand);
    }

    [Fact]
    public async Task InsufficientStock_ThrowsValidationException()
    {
        var (profile, loc) = await SetupProfileAndLocationAsync(allowNegative: false);
        
        var req = new StockUsageDto { InventoryProfileId = profile.Id, InventoryLocationId = loc.Id, Quantity = 5 };
        await Assert.ThrowsAsync<ValidationException>(() => _service.RecordUsageAsync(req));
    }
    
    [Fact]
    public async Task ZeroQuantity_ThrowsValidationException()
    {
        var (profile, loc) = await SetupProfileAndLocationAsync();
        
        var req = new StockReceiptDto { InventoryProfileId = profile.Id, InventoryLocationId = loc.Id, Quantity = 0 };
        await Assert.ThrowsAsync<ValidationException>(() => _service.ReceiveStockAsync(req));
    }

    [Fact]
    public async Task Transfer_CreatesInAndOutMovements()
    {
        var (profile, loc1) = await SetupProfileAndLocationAsync();
        var loc2 = await _service.CreateLocationAsync(new CreateInventoryLocationDto { Name = "Loc2" });
        
        await _service.ReceiveStockAsync(new StockReceiptDto { InventoryProfileId = profile.Id, InventoryLocationId = loc1.Id, Quantity = 10 });
        
        var req = new StockTransferDto { InventoryProfileId = profile.Id, SourceLocationId = loc1.Id, DestinationLocationId = loc2.Id, Quantity = 4 };
        var movs = await _service.TransferStockAsync(req);
        
        Assert.Equal(2, movs.Count);
        
        var outMov = movs.Single(m => m.MovementType == InventoryMovementType.TransferOut);
        var inMov = movs.Single(m => m.MovementType == InventoryMovementType.TransferIn);
        
        Assert.Equal(-4, outMov.Quantity);
        Assert.Equal(4, inMov.Quantity);
        
        var b1 = await _context.InventoryStockLevels.FirstAsync(b => b.InventoryLocationId == loc1.Id);
        var b2 = await _context.InventoryStockLevels.FirstAsync(b => b.InventoryLocationId == loc2.Id);
        
        Assert.Equal(6, b1.QuantityOnHand);
        Assert.Equal(4, b2.QuantityOnHand);
    }
    
    [Fact]
    public async Task Transfer_SourceEqualsDestination_ThrowsValidationException()
    {
        var (profile, loc) = await SetupProfileAndLocationAsync();
        var req = new StockTransferDto { InventoryProfileId = profile.Id, SourceLocationId = loc.Id, DestinationLocationId = loc.Id, Quantity = 1 };
        await Assert.ThrowsAsync<ValidationException>(() => _service.TransferStockAsync(req));
    }

    [Fact]
    public async Task LotTracking_Enforced()
    {
        var (profile, loc) = await SetupProfileAndLocationAsync(trackLots: true);
        var req = new StockReceiptDto { InventoryProfileId = profile.Id, InventoryLocationId = loc.Id, Quantity = 10 };
        await Assert.ThrowsAsync<ValidationException>(() => _service.ReceiveStockAsync(req));
        
        req.NewLot = new CreateInventoryLotDto { LotNumber = "LOT1" };
        await _service.ReceiveStockAsync(req);
        Assert.Equal(1, await _context.InventoryLots.CountAsync());
    }

    [Fact]
    public async Task Expiration_RequiredIfTracked()
    {
        var (profile, loc) = await SetupProfileAndLocationAsync(trackLots: true, trackExpiration: true);
        var req = new StockReceiptDto { InventoryProfileId = profile.Id, InventoryLocationId = loc.Id, Quantity = 10, NewLot = new CreateInventoryLotDto { LotNumber = "LOT1" } };
        await Assert.ThrowsAsync<ValidationException>(() => _service.ReceiveStockAsync(req));
        
        req.NewLot.ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        await _service.ReceiveStockAsync(req);
        Assert.Equal(1, await _context.InventoryLots.CountAsync());
    }
    
    [Fact]
    public async Task LowStockCalculation_Works()
    {
        var (profile, loc) = await SetupProfileAndLocationAsync(reorderLevel: 5);
        await _service.ReceiveStockAsync(new StockReceiptDto { InventoryProfileId = profile.Id, InventoryLocationId = loc.Id, Quantity = 4 });
        
        var low = await _service.GetLowStockProfilesAsync();
        Assert.Single(low);
        Assert.Equal(profile.Id, low[0].Id);
        
        await _service.ReceiveStockAsync(new StockReceiptDto { InventoryProfileId = profile.Id, InventoryLocationId = loc.Id, Quantity = 2 });
        low = await _service.GetLowStockProfilesAsync();
        Assert.Empty(low);
    }
    
    [Fact]
    public async Task ExpiringLotsQuery_Works()
    {
        var (profile, loc) = await SetupProfileAndLocationAsync(trackLots: true, trackExpiration: true);
        await _service.ReceiveStockAsync(new StockReceiptDto { InventoryProfileId = profile.Id, InventoryLocationId = loc.Id, Quantity = 10, NewLot = new CreateInventoryLotDto { LotNumber = "LOT1", ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)) } });
        await _service.ReceiveStockAsync(new StockReceiptDto { InventoryProfileId = profile.Id, InventoryLocationId = loc.Id, Quantity = 10, NewLot = new CreateInventoryLotDto { LotNumber = "LOT2", ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)) } });

        var expiring = await _service.GetExpiringLotsAsync(30);
        Assert.Single(expiring);
        Assert.Equal("LOT1", expiring[0].LotNumber);
    }
    
    [Fact]
    public async Task CrossTenant_IsolationEnforced()
    {
        var otherBusinessId = Guid.NewGuid();
        var product = new CatalogItem { Id = Guid.NewGuid(), BusinessId = otherBusinessId, Type = CatalogItemType.Product, Name = "Prod" };
        var profile = new InventoryProfile { Id = Guid.NewGuid(), BusinessId = otherBusinessId, CatalogItemId = product.Id, ReorderLevel = 5, CatalogItem = product };
        _context.CatalogItems.Add(product);
        _context.InventoryProfiles.Add(profile);
        await _context.SaveChangesAsync();

        var req = new StockReceiptDto { InventoryProfileId = profile.Id, InventoryLocationId = Guid.NewGuid(), Quantity = 10 };
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ReceiveStockAsync(req));
    }

    [Fact]
    public async Task ReceiveStock_TenantBLocation_IsRejectedWithoutPersistingInventoryRows()
    {
        var (profile, _) = await SetupProfileAndLocationAsync();
        var otherLocationId = await SeedOtherTenantLocationAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReceiveStockAsync(new StockReceiptDto
            {
                InventoryProfileId = profile.Id,
                InventoryLocationId = otherLocationId,
                Quantity = 5
            }));

        Assert.Equal(0, await _context.InventoryMovements.CountAsync());
        Assert.Equal(0, await _context.InventoryStockLevels.CountAsync());
    }

    [Fact]
    public async Task RecordUsage_TenantBLocation_IsRejectedWithoutPersistingInventoryRows()
    {
        var (profile, _) = await SetupProfileAndLocationAsync(allowNegative: true);
        var otherLocationId = await SeedOtherTenantLocationAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.RecordUsageAsync(new StockUsageDto
            {
                InventoryProfileId = profile.Id,
                InventoryLocationId = otherLocationId,
                Quantity = 1
            }));

        Assert.Equal(0, await _context.InventoryMovements.CountAsync());
        Assert.Equal(0, await _context.InventoryStockLevels.CountAsync());
    }

    [Fact]
    public async Task RecordWaste_TenantBLocation_IsRejectedWithoutPersistingInventoryRows()
    {
        var (profile, _) = await SetupProfileAndLocationAsync(allowNegative: true);
        var otherLocationId = await SeedOtherTenantLocationAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.RecordWasteAsync(new StockWasteDto
            {
                InventoryProfileId = profile.Id,
                InventoryLocationId = otherLocationId,
                Quantity = 1
            }));

        Assert.Equal(0, await _context.InventoryMovements.CountAsync());
        Assert.Equal(0, await _context.InventoryStockLevels.CountAsync());
    }

    [Fact]
    public async Task AdjustStock_TenantBLocation_IsRejectedWithoutPersistingInventoryRows()
    {
        var (profile, _) = await SetupProfileAndLocationAsync();
        var otherLocationId = await SeedOtherTenantLocationAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.AdjustStockAsync(new StockAdjustmentDto
            {
                InventoryProfileId = profile.Id,
                InventoryLocationId = otherLocationId,
                QuantityDifference = 1
            }));

        Assert.Equal(0, await _context.InventoryMovements.CountAsync());
        Assert.Equal(0, await _context.InventoryStockLevels.CountAsync());
    }

    [Fact]
    public async Task TransferStock_TenantBSourceLocation_IsRejectedWithoutPersistingNewInventoryRows()
    {
        var (profile, tenantLocation) = await SetupProfileAndLocationAsync();
        var otherLocationId = await SeedOtherTenantLocationAsync();
        var movementCount = await _context.InventoryMovements.CountAsync();
        var stockLevelCount = await _context.InventoryStockLevels.CountAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.TransferStockAsync(new StockTransferDto
            {
                InventoryProfileId = profile.Id,
                SourceLocationId = otherLocationId,
                DestinationLocationId = tenantLocation.Id,
                Quantity = 1
            }));

        Assert.Equal(movementCount, await _context.InventoryMovements.CountAsync());
        Assert.Equal(stockLevelCount, await _context.InventoryStockLevels.CountAsync());
    }

    [Fact]
    public async Task TransferStock_TenantBDestinationLocation_IsRejectedWithoutPersistingNewInventoryRows()
    {
        var (profile, tenantLocation) = await SetupProfileAndLocationAsync();
        await _service.ReceiveStockAsync(new StockReceiptDto
        {
            InventoryProfileId = profile.Id,
            InventoryLocationId = tenantLocation.Id,
            Quantity = 5
        });
        var otherLocationId = await SeedOtherTenantLocationAsync();
        var movementCount = await _context.InventoryMovements.CountAsync();
        var stockLevelCount = await _context.InventoryStockLevels.CountAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.TransferStockAsync(new StockTransferDto
            {
                InventoryProfileId = profile.Id,
                SourceLocationId = tenantLocation.Id,
                DestinationLocationId = otherLocationId,
                Quantity = 1
            }));

        Assert.Equal(movementCount, await _context.InventoryMovements.CountAsync());
        Assert.Equal(stockLevelCount, await _context.InventoryStockLevels.CountAsync());
    }

    private async Task<(InventoryProfileDto profile, InventoryLocationDto loc)> SetupProfileAndLocationAsync(
        bool trackLots = false, bool trackExpiration = false, bool allowNegative = false, decimal reorderLevel = 0)
    {
        var product = new CatalogItem { Id = Guid.NewGuid(), BusinessId = _businessId, Type = CatalogItemType.Product, Name = "Prod" };
        _context.CatalogItems.Add(product);
        await _context.SaveChangesAsync();
        
        var profile = await _service.CreateInventoryProfileAsync(new CreateInventoryProfileDto { CatalogItemId = product.Id, TrackLots = trackLots, TrackExpiration = trackExpiration, AllowNegativeStock = allowNegative, ReorderLevel = reorderLevel });
        var loc = await _service.CreateLocationAsync(new CreateInventoryLocationDto { Name = "Main" });
        return (profile, loc);
    }

    private async Task<Guid> SeedOtherTenantLocationAsync()
    {
        var location = new InventoryLocation
        {
            Id = Guid.NewGuid(),
            BusinessId = Guid.NewGuid(),
            Name = "Other Tenant Location",
            IsActive = true
        };

        _context.InventoryLocations.Add(location);
        await _context.SaveChangesAsync();
        return location.Id;
    }
    
    
    [Fact]

    public void Dispose()
    {
        // removed
        _context.Dispose();
    }
}
