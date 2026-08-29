using Microsoft.EntityFrameworkCore;
using Moq;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Infrastructure.Data;
using Xunit;

namespace SmallBusiness.Application.Tests;

public class TenantIsolationTests
{
    private static (ApplicationDbContext Context, Mock<ITenantContext> MockUser) CreateInMemoryContextAndUser()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mockUser = new Mock<ITenantContext>();
        var context = new ApplicationDbContext(options, mockUser.Object);
        return (context, mockUser);
    }
    
    private static ApplicationDbContext CreateInMemoryContextWithoutUser()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task SeedDataAsync(ApplicationDbContext context, Guid businessA, Guid businessB)
    {
        context.Activities.AddRange(
            new Activity { Id = Guid.NewGuid(), BusinessId = businessA, Description = "Action A1", EntityType = "Test", CreatedAt = DateTime.UtcNow },
            new Activity { Id = Guid.NewGuid(), BusinessId = businessA, Description = "Action A2", EntityType = "Test", CreatedAt = DateTime.UtcNow },
            new Activity { Id = Guid.NewGuid(), BusinessId = businessB, Description = "Action B1", EntityType = "Test", CreatedAt = DateTime.UtcNow }
        );

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task NormalUser_WithValidCurrentBusinessId_SeesOnlyOwnRecords()
    {
        // Arrange
        var businessA = Guid.NewGuid();
        var businessB = Guid.NewGuid();
        var (context, mockUser) = CreateInMemoryContextAndUser();
        await SeedDataAsync(context, businessA, businessB);

        // Act: Normal user for Business A
        mockUser.Setup(x => x.IsCrossTenantAdmin).Returns(false);
        mockUser.Setup(x => x.CurrentBusinessId).Returns(businessA);

        var activities = await context.Activities.ToListAsync();

        // Assert
        Assert.Equal(2, activities.Count);
        Assert.All(activities, a => Assert.Equal(businessA, a.BusinessId));
    }

    [Fact]
    public async Task NormalUser_WithNullCurrentBusinessId_SeesNoRecords()
    {
        // Arrange
        var businessA = Guid.NewGuid();
        var businessB = Guid.NewGuid();
        var (context, mockUser) = CreateInMemoryContextAndUser();
        await SeedDataAsync(context, businessA, businessB);

        // Act: Normal user but no active business
        mockUser.Setup(x => x.IsCrossTenantAdmin).Returns(false);
        mockUser.Setup(x => x.CurrentBusinessId).Returns((Guid?)null);

        var activities = await context.Activities.ToListAsync();

        // Assert
        Assert.Empty(activities);
    }

    [Fact]
    public async Task CrossTenantSysAdmin_SeesAllRecords()
    {
        // Arrange
        var businessA = Guid.NewGuid();
        var businessB = Guid.NewGuid();
        var (context, mockUser) = CreateInMemoryContextAndUser();
        await SeedDataAsync(context, businessA, businessB);

        // Act: SysAdmin
        mockUser.Setup(x => x.IsCrossTenantAdmin).Returns(true);
        mockUser.Setup(x => x.CurrentBusinessId).Returns((Guid?)null);

        var activities = await context.Activities.ToListAsync();

        // Assert
        Assert.Equal(3, activities.Count);
    }

    [Fact]
    public async Task Unauthenticated_Or_NoTenantContext_SeesNoRecords()
    {
        // Arrange
        var businessA = Guid.NewGuid();
        var businessB = Guid.NewGuid();
        await using var context = CreateInMemoryContextWithoutUser();
        
        await SeedDataAsync(context, businessA, businessB);

        // Act
        var activities = await context.Activities.ToListAsync();

        // Assert
        Assert.Empty(activities);
    }
}
