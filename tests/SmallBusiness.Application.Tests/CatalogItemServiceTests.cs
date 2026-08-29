using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmallBusiness.Application.DTOs.CatalogItems;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Application.Services;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;
using SmallBusiness.Infrastructure.Data;
using System.ComponentModel.DataAnnotations;

namespace SmallBusiness.Application.Tests;

public class CatalogItemServiceTests
{
    private static ApplicationDbContext CreateInMemoryContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, tenantContext);
    }

    [Fact]
    public async Task CreateCatalogItemAsync_WithValidContext_SetsBusinessIdAndGeneratesCode()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        mockContext.Setup(x => x.UserId).Returns("user1");

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockSequenceService = new Mock<ITenantSequenceService>();
        mockSequenceService.Setup(x => x.GetNextItemCodeAsync()).ReturnsAsync("ITEM-000001");

        var service = new CatalogItemService(dbContext, mockContext.Object, mockSequenceService.Object);

        var request = new CreateCatalogItemRequest
        {
            Name = "Air Filter",
            Type = CatalogItemType.Product,
            Unit = "Each",
            Cost = 5.00m,
            SellingPrice = 15.00m,
            Taxable = true
        };

        // Act
        var itemId = await service.CreateCatalogItemAsync(request);

        // Assert
        var item = await dbContext.CatalogItems.IgnoreQueryFilters().FirstAsync(i => i.Id == itemId);
        Assert.Equal(businessId, item.BusinessId);
        Assert.Equal("ITEM-000001", item.ItemCode);
        Assert.Equal("Air Filter", item.Name);
        Assert.True(item.IsActive);
    }

    [Fact]
    public async Task CreateCatalogItemAsync_WithManualCode_UsesManualCode()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        mockContext.Setup(x => x.UserId).Returns("user1");

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockSequenceService = new Mock<ITenantSequenceService>();

        var service = new CatalogItemService(dbContext, mockContext.Object, mockSequenceService.Object);

        var request = new CreateCatalogItemRequest
        {
            ItemCode = "MANUAL-001 ", // Should trim whitespace
            Name = "Air Filter",
            Type = CatalogItemType.Product,
            Unit = "Each"
        };

        // Act
        var itemId = await service.CreateCatalogItemAsync(request);

        // Assert
        var item = await dbContext.CatalogItems.IgnoreQueryFilters().FirstAsync(i => i.Id == itemId);
        Assert.Equal("MANUAL-001", item.ItemCode); // Trimmed
    }

    [Fact]
    public async Task CreateCatalogItemAsync_DuplicateManualCode_ThrowsValidationException()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        mockContext.Setup(x => x.UserId).Returns("user1");

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        dbContext.CatalogItems.Add(new CatalogItem
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            ItemCode = "DUP-001",
            Name = "Existing",
            Type = CatalogItemType.Product,
            Unit = "Each"
        });
        await dbContext.SaveChangesAsync();

        var mockSequenceService = new Mock<ITenantSequenceService>();
        var service = new CatalogItemService(dbContext, mockContext.Object, mockSequenceService.Object);

        var request = new CreateCatalogItemRequest
        {
            ItemCode = "DUP-001",
            Name = "New Item",
            Type = CatalogItemType.Product,
            Unit = "Each"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateCatalogItemAsync(request));
    }

    [Fact]
    public async Task CreateCatalogItemAsync_NegativePrice_ThrowsValidationException()
    {
        // Arrange
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(Guid.NewGuid());

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var sequenceService = new Mock<ITenantSequenceService>();
        var service = new CatalogItemService(dbContext, mockContext.Object, sequenceService.Object);

        var request = new CreateCatalogItemRequest
        {
            Name = "Item",
            Type = CatalogItemType.Product,
            Unit = "Each",
            Cost = -5.00m,
            SellingPrice = 0 // zero is allowed, but negative cost is not
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateCatalogItemAsync(request));
    }

    [Fact]
    public async Task GetCatalogItemAsync_FromDifferentTenant_ThrowsKeyNotFound()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        
        var mockContextA = new Mock<ITenantContext>();
        mockContextA.Setup(x => x.CurrentBusinessId).Returns(tenantA);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "SharedDB_Catalog")
            .Options;
            
        await using var contextA = new ApplicationDbContext(options, mockContextA.Object);
        var item = new CatalogItem
        {
            Id = Guid.NewGuid(),
            BusinessId = tenantA,
            ItemCode = "ITEM-001",
            Name = "Tenant A Item",
            Type = CatalogItemType.Product,
            Unit = "Each"
        };
        contextA.CatalogItems.Add(item);
        await contextA.SaveChangesAsync();

        var mockContextB = new Mock<ITenantContext>();
        mockContextB.Setup(x => x.CurrentBusinessId).Returns(tenantB);
        await using var contextB = new ApplicationDbContext(options, mockContextB.Object);
        
        var sequenceService = new Mock<ITenantSequenceService>();
        var serviceB = new CatalogItemService(contextB, mockContextB.Object, sequenceService.Object);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => serviceB.GetCatalogItemAsync(item.Id));
    }

    [Fact]
    public async Task DeactivateCatalogItemAsync_SetsIsActiveFalseAndCreatesActivity()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        mockContext.Setup(x => x.UserId).Returns("user1");

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var item = new CatalogItem
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            ItemCode = "ITEM-001",
            Name = "Item",
            Type = CatalogItemType.Product,
            Unit = "Each",
            IsActive = true
        };
        dbContext.CatalogItems.Add(item);
        await dbContext.SaveChangesAsync();

        var sequenceService = new Mock<ITenantSequenceService>();
        var service = new CatalogItemService(dbContext, mockContext.Object, sequenceService.Object);

        // Act
        await service.DeactivateCatalogItemAsync(item.Id);

        // Assert
        var updatedItem = await dbContext.CatalogItems.IgnoreQueryFilters().FirstAsync(i => i.Id == item.Id);
        Assert.False(updatedItem.IsActive);

        var activity = await dbContext.Activities.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.EntityId == item.Id && a.ActivityType == ActivityType.StatusChanged);
        Assert.NotNull(activity);
    }
}
