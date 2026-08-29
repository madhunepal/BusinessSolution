using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Moq;
using SmallBusiness.Application.DTOs.Customers;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Application.Services;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;
using SmallBusiness.Infrastructure.Data;

namespace SmallBusiness.Application.Tests;

public class CustomerServiceTests
{
    private static ApplicationDbContext CreateInMemoryContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, tenantContext);
    }

    [Fact]
    public async Task CreateCustomerAsync_WithValidContext_SetsBusinessIdAndGeneratesNumber()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        mockContext.Setup(x => x.UserId).Returns("user1");

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        
        mockServiceProvider.Setup(x => x.GetService(typeof(IApplicationDbContext))).Returns(dbContext);
        mockScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
        mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);

        var sequenceService = new TenantSequenceService(mockScopeFactory.Object, mockContext.Object);
        var service = new CustomerService(dbContext, mockContext.Object, sequenceService);

        var request = new CreateCustomerRequest
        {
            Name = "Acme Corp",
            CustomerType = CustomerType.Business
        };

        // Act
        var customerId = await service.CreateCustomerAsync(request);

        // Assert
        var customer = await dbContext.Customers.IgnoreQueryFilters().FirstAsync(c => c.Id == customerId);
        Assert.Equal(businessId, customer.BusinessId);
        Assert.Equal("CUST-000001", customer.CustomerNumber);
        Assert.Equal("Acme Corp", customer.Name);
        Assert.True(customer.IsActive);
    }

    [Fact]
    public async Task CreateCustomerAsync_NoTenantContext_ThrowsUnauthorized()
    {
        // Arrange
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns((Guid?)null); // No tenant

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        
        mockServiceProvider.Setup(x => x.GetService(typeof(IApplicationDbContext))).Returns(dbContext);
        mockScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
        mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);

        var sequenceService = new TenantSequenceService(mockScopeFactory.Object, mockContext.Object);
        var service = new CustomerService(dbContext, mockContext.Object, sequenceService);

        var request = new CreateCustomerRequest { Name = "Acme Corp" };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateCustomerAsync(request));
    }

    [Fact]
    public async Task GetCustomerAsync_FromDifferentTenant_ThrowsKeyNotFound()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        
        var mockContextA = new Mock<ITenantContext>();
        mockContextA.Setup(x => x.CurrentBusinessId).Returns(tenantA);

        await using var dbContextA = CreateInMemoryContext(mockContextA.Object);
        
        // Add customer for Tenant A directly
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            BusinessId = tenantA,
            Name = "Tenant A Customer",
            CustomerNumber = "CUST-000001",
            CustomerType = CustomerType.Business
        };
        dbContextA.Customers.Add(customer);
        await dbContextA.SaveChangesAsync();

        // Now simulate Tenant B context using the SAME database
        var mockContextB = new Mock<ITenantContext>();
        mockContextB.Setup(x => x.CurrentBusinessId).Returns(tenantB);
        
        var dbContextB = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbContextA.Database.ProviderName!).Options, 
            mockContextB.Object); // The memory database name wasn't explicitly saved, let's reuse dbContextA's instance but this won't change the injected tenant context for EF Core filters which is set on construction.
            
        // Wait, EF Core DbContext instances lock the injected tenantContext.
        // Better way: Create another context connected to the same InMemory DB but with different ITenantContext.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "SharedDB")
            .Options;
            
        await using var contextA = new ApplicationDbContext(options, mockContextA.Object);
        if (!await contextA.Customers.AnyAsync()) 
        {
            contextA.Customers.Add(new Customer { Id = customer.Id, BusinessId = tenantA, Name = "C1", CustomerNumber = "C1", CustomerType = CustomerType.Business });
            await contextA.SaveChangesAsync();
        }

        await using var contextB = new ApplicationDbContext(options, mockContextB.Object);
        var sequenceService = new Mock<ITenantSequenceService>();
        var serviceB = new CustomerService(contextB, mockContextB.Object, sequenceService.Object);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => serviceB.GetCustomerAsync(customer.Id));
    }

    [Fact]
    public async Task CreateCustomerAsync_InvalidRequest_ThrowsValidationException()
    {
        // Arrange
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(Guid.NewGuid());

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var sequenceService = new Mock<ITenantSequenceService>();
        var service = new CustomerService(dbContext, mockContext.Object, sequenceService.Object);

        // Name is required, this should fail validation
        var request = new CreateCustomerRequest
        {
            Name = "" 
        };

        // Act & Assert
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.CreateCustomerAsync(request));
    }

    [Fact]
    public async Task CreateCustomerAsync_GeneratesActivityRecord()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        mockContext.Setup(x => x.UserId).Returns("user1");

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var sequenceService = new Mock<ITenantSequenceService>();
        sequenceService.Setup(x => x.GetNextCustomerNumberAsync()).ReturnsAsync("CUST-000001");
        
        var service = new CustomerService(dbContext, mockContext.Object, sequenceService.Object);

        var request = new CreateCustomerRequest { Name = "Acme Corp" };

        // Act
        var customerId = await service.CreateCustomerAsync(request);

        // Assert
        var activity = await dbContext.Activities.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.EntityId == customerId);
        Assert.NotNull(activity);
        Assert.Equal(ActivityType.Created, activity.ActivityType);
        Assert.Equal("Customer", activity.EntityType);
        Assert.Equal(businessId, activity.BusinessId);
    }

    [Fact]
    public async Task UpdateCustomerAsync_GeneratesActivityRecord()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        mockContext.Setup(x => x.UserId).Returns("user1");

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var customer = new Customer { Id = Guid.NewGuid(), BusinessId = businessId, Name = "Acme", CustomerNumber = "CUST-000001", CustomerType = CustomerType.Business };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var sequenceService = new Mock<ITenantSequenceService>();
        var service = new CustomerService(dbContext, mockContext.Object, sequenceService.Object);

        var request = new UpdateCustomerRequest { Name = "Acme Updated", CustomerType = CustomerType.Business };

        // Act
        await service.UpdateCustomerAsync(customer.Id, request);

        // Assert
        var activity = await dbContext.Activities.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.EntityId == customer.Id && a.ActivityType == ActivityType.Updated);
        Assert.NotNull(activity);
        Assert.Equal(businessId, activity.BusinessId);
    }

    [Fact]
    public async Task GetNextCustomerNumberAsync_ConcurrentRequests_GeneratesUniqueNumbers()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        mockContext.Setup(x => x.UserId).Returns("user1");

        // We need a thread-safe InMemory DB collection to simulate concurrent saves safely.
        // Actually, EF Core InMemory DB is not thread-safe for concurrent SaveChanges.
        // But for testing purposes, we can spin up multiple scoped DbContexts to the same InMemory database name
        // and invoke GetNextCustomerNumberAsync concurrently to see if it yields unique numbers.
        
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ITenantContext>(_ => mockContext.Object);
        var provider = services.BuildServiceProvider();

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var sequenceService = new TenantSequenceService(scopeFactory, mockContext.Object);

        // Act
        int requestCount = 10;
        var tasks = Enumerable.Range(0, requestCount).Select(_ => sequenceService.GetNextCustomerNumberAsync());
        
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(requestCount, results.Length);
        Assert.Equal(requestCount, results.Distinct().Count()); // All must be unique
    }
}
