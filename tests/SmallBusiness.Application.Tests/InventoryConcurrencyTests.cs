using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
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

public class InventoryConcurrencyTests : IDisposable
{
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly string _userId = "test-user-id";
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly string _dbName;

    public InventoryConcurrencyTests()
    {
        _dbName = Guid.NewGuid().ToString();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(_dbName)
            .AddInterceptors(new InMemoryConcurrencyInterceptor())
            .Options;

        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(x => x.CurrentBusinessId).Returns(_businessId);
        _mockTenantContext.Setup(x => x.UserId).Returns(_userId);

        using var setupContext = new ApplicationDbContext(_options, _mockTenantContext.Object);
        setupContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task ConcurrentReductions_CannotOversellStock_WithInMemoryInterceptor()
    {
        Guid profileId, locationId;
        using (var setupContext = new ApplicationDbContext(_options, _mockTenantContext.Object))
        {
            var setupService = new InventoryService(setupContext, _mockTenantContext.Object);
            
            var business = new Business { Id = _businessId, Name = "Test Business" };
            setupContext.Businesses.Add(business);
            
            var product = new CatalogItem { Id = Guid.NewGuid(), BusinessId = _businessId, Type = CatalogItemType.Product, Name = "Prod" };
            setupContext.CatalogItems.Add(product);
            await setupContext.SaveChangesAsync();
            
            var profile = await setupService.CreateInventoryProfileAsync(new CreateInventoryProfileDto { CatalogItemId = product.Id, AllowNegativeStock = false });
            var loc = await setupService.CreateLocationAsync(new CreateInventoryLocationDto { Name = "Main" });
            
            await setupService.ReceiveStockAsync(new StockReceiptDto { InventoryProfileId = profile.Id, InventoryLocationId = loc.Id, Quantity = 5 });
            
            profileId = profile.Id;
            locationId = loc.Id;
        }

        // Context A and Context B simulate two parallel requests
        using var contextA = new ApplicationDbContext(_options, _mockTenantContext.Object);
        using var contextB = new ApplicationDbContext(_options, _mockTenantContext.Object);
        
        var serviceA = new InventoryService(contextA, _mockTenantContext.Object);
        var serviceB = new InventoryService(contextB, _mockTenantContext.Object);

        // Pre-load entities in both contexts so they both have Stale Data with original RowVersion
        var bucketA = await contextA.InventoryStockLevels.FirstAsync();
        var bucketB = await contextB.InventoryStockLevels.FirstAsync();
        
        // Assert they read the same initial stock
        Assert.Equal(5, bucketA.QuantityOnHand);
        Assert.Equal(5, bucketB.QuantityOnHand);
        
        var reqA = new StockUsageDto { InventoryProfileId = profileId, InventoryLocationId = locationId, Quantity = 4 };
        var reqB = new StockUsageDto { InventoryProfileId = profileId, InventoryLocationId = locationId, Quantity = 4 };

        // A commits reduction successfully. Interceptor updates RowVersion.
        await serviceA.RecordUsageAsync(reqA);
        
        // B attempts reduction. 
        // Service B queries DbContext B, which returns the tracked `bucketB`.
        // It modifies `bucketB` and saves. The interceptor sees RowVersion is outdated,
        // and throws DbUpdateConcurrencyException.
        // Service B catches it, clears the local cache, retries, loads the fresh data (qty=1).
        // Then 1 - 4 = -3. AllowNegativeStock is false, so it throws ValidationException!
        
        var exception = await Record.ExceptionAsync(() => serviceB.RecordUsageAsync(reqB));
        
        Assert.NotNull(exception);
        Assert.IsType<ValidationException>(exception);
        Assert.Contains("Insufficient stock", exception.Message);

        using var verifyContext = new ApplicationDbContext(_options, _mockTenantContext.Object);
        var finalBucket = await verifyContext.InventoryStockLevels.FirstAsync();
        var finalMovements = await verifyContext.InventoryMovements.ToListAsync();

        Assert.Equal(1, finalBucket.QuantityOnHand);
        // 1 receipt + 1 successful reduction
        Assert.Equal(2, finalMovements.Count);
    }

    public void Dispose()
    {
    }
}
