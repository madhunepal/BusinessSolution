using Microsoft.EntityFrameworkCore;
using Moq;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Application.Services;
using SmallBusiness.Infrastructure.Data;

namespace SmallBusiness.Application.Tests;

public class BusinessServiceTests
{
    private static ApplicationDbContext CreateInMemoryContext(ITenantContext? currentUserService = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        if (currentUserService != null)
        {
            return new ApplicationDbContext(options, currentUserService);
        }

        // Create a default mock with no tenant filter (CurrentBusinessId = null)
        var defaultUser = new Mock<ITenantContext>();
        defaultUser.Setup(x => x.CurrentBusinessId).Returns((Guid?)null);
        return new ApplicationDbContext(options, defaultUser.Object);
    }

    [Fact]
    public async Task CreateBusinessAsync_WithValidData_Succeeds()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var currentUser = new Mock<ITenantContext>();
        currentUser.Setup(x => x.UserId).Returns("user-123");

        var service = new BusinessService(context, currentUser.Object);

        // Act
        var result = await service.CreateBusinessAsync("Test HVAC LLC", "555-0100", "info@testhvac.com");

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotEqual(Guid.Empty, result.Value);

        var business = await context.Businesses.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == result.Value);
        Assert.NotNull(business);
        Assert.Equal("Test HVAC LLC", business.Name);

        var businessUser = await context.BusinessUsers.IgnoreQueryFilters().FirstAsync(bu => bu.BusinessId == result.Value);
        Assert.Equal("user-123", businessUser.UserId);
        Assert.Equal("Owner", businessUser.Role);
    }

    [Fact]
    public async Task CreateBusinessAsync_WithEmptyName_Fails()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var currentUser = new Mock<ITenantContext>();
        currentUser.Setup(x => x.UserId).Returns("user-123");

        var service = new BusinessService(context, currentUser.Object);

        // Act
        var result = await service.CreateBusinessAsync("");

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Business name is required.", result.Errors);
    }

    [Fact]
    public async Task CreateBusinessAsync_WithWhitespaceName_Fails()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var currentUser = new Mock<ITenantContext>();
        currentUser.Setup(x => x.UserId).Returns("user-123");

        var service = new BusinessService(context, currentUser.Object);

        // Act
        var result = await service.CreateBusinessAsync("   ");

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Business name is required.", result.Errors);
    }

    [Fact]
    public async Task CreateBusinessAsync_WithNoUser_Fails()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var currentUser = new Mock<ITenantContext>();
        currentUser.Setup(x => x.UserId).Returns((string?)null);

        var service = new BusinessService(context, currentUser.Object);

        // Act
        var result = await service.CreateBusinessAsync("Test Co");

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("User must be authenticated to create a business.", result.Errors);
    }

    [Fact]
    public async Task GetCurrentBusinessAsync_WithNoBusiness_Fails()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var currentUser = new Mock<ITenantContext>();
        currentUser.Setup(x => x.CurrentBusinessId).Returns((Guid?)null);

        var service = new BusinessService(context, currentUser.Object);

        // Act
        var result = await service.GetCurrentBusinessAsync();

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("No active business selected.", result.Errors);
    }

    [Fact]
    public async Task CreateBusinessAsync_TrimsBusiness_Name()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var currentUser = new Mock<ITenantContext>();
        currentUser.Setup(x => x.UserId).Returns("user-123");

        var service = new BusinessService(context, currentUser.Object);

        // Act
        var result = await service.CreateBusinessAsync("  Test Co  ");

        // Assert
        Assert.True(result.Succeeded);
        var business = await context.Businesses.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == result.Value);
        Assert.Equal("Test Co", business!.Name);
    }
}
