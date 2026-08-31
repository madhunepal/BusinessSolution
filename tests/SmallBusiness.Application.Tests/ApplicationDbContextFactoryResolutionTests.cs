using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Application.Services;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Infrastructure;
using SmallBusiness.Infrastructure.Data;

namespace SmallBusiness.Application.Tests;

public class ApplicationDbContextFactoryResolutionTests
{
    [Fact]
    public async Task RealServiceCollection_DbContextFactoryAndFactoryBackedServicesResolve()
    {
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.CurrentBusinessId).Returns(Guid.NewGuid());
        tenantContext.Setup(t => t.UserId).Returns("user-id");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(local);Database=SmallBusinessFactoryResolutionTests;Trusted_Connection=True;TrustServerCertificate=True"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(tenantContext.Object);
        services.AddInfrastructure(configuration);
        services.AddApplicationServices();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var efFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await efFactory.CreateDbContextAsync();

        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IInventoryService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAppointmentService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<BusinessService>());
    }

    [Fact]
    public async Task DbContextFactory_CreatedContextUsesRegisteredTenantContextForFilters()
    {
        var businessA = Guid.NewGuid();
        var businessB = Guid.NewGuid();
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.CurrentBusinessId).Returns(businessA);
        tenantContext.Setup(t => t.UserId).Returns("user-id");

        var services = new ServiceCollection();
        services.AddSingleton(tenantContext.Object);
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddDbContextFactory<ApplicationDbContext>(
            options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()),
            ServiceLifetime.Scoped);
        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IApplicationDbContextFactory, ApplicationDbContextFactory>();
        services.AddScoped<BusinessService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IAppointmentService, AppointmentService>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var efFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await efFactory.CreateDbContextAsync();

        context.Businesses.AddRange(
            new Business { Id = businessA, Name = "Tenant A" },
            new Business { Id = businessB, Name = "Tenant B" });
        context.Customers.AddRange(
            new Customer { Id = Guid.NewGuid(), BusinessId = businessA, CustomerNumber = "A-1", Name = "Tenant A Customer" },
            new Customer { Id = Guid.NewGuid(), BusinessId = businessB, CustomerNumber = "B-1", Name = "Tenant B Customer" });
        await context.SaveChangesAsync();

        var visibleCustomers = await context.Customers.ToListAsync();

        Assert.Single(visibleCustomers);
        Assert.Equal(businessA, visibleCustomers.Single().BusinessId);
    }
}
