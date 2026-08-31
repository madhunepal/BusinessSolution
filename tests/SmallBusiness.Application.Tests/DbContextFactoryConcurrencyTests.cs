using Microsoft.EntityFrameworkCore;
using Moq;
using SmallBusiness.Application.DTOs.Appointments;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Application.Services;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;
using SmallBusiness.Infrastructure.Data;

namespace SmallBusiness.Application.Tests;

public class DbContextFactoryConcurrencyTests
{
    [Fact]
    public async Task InventoryService_OverlappingReads_UseIndependentDbContextsAndApplyTenantFilter()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var tenantContext = MockTenant(tenantA);
        var options = CreateOptions();
        await SeedInventoryAsync(options, tenantContext.Object, tenantA, "TENANT-A");
        await SeedInventoryAsync(options, tenantContext.Object, tenantB, "TENANT-B");
        var factory = new TestApplicationDbContextFactory(options, tenantContext.Object);
        var service = new InventoryService(factory, tenantContext.Object);

        var profilesTask = service.GetInventoryProfilesAsync();
        var locationsTask = service.GetLocationsAsync();
        await Task.WhenAll(profilesTask, locationsTask);
        var profiles = await profilesTask;
        var locations = await locationsTask;

        Assert.True(factory.CreatedContextCount >= 2);
        Assert.Single(profiles);
        Assert.Equal("TENANT-A", profiles.Single().ItemCode);
        Assert.Single(locations);
    }

    [Fact]
    public async Task AppointmentService_OverlappingReads_UseIndependentDbContexts()
    {
        var businessId = Guid.NewGuid();
        var tenantContext = MockTenant(businessId);
        var options = CreateOptions();
        await SeedAppointmentAsync(options, tenantContext.Object, businessId);
        var factory = new TestApplicationDbContextFactory(options, tenantContext.Object);
        var service = new AppointmentService(factory, tenantContext.Object);

        await using var readContext = new ApplicationDbContext(options, tenantContext.Object);
        var id = await readContext.Appointments.Select(a => a.Id).SingleAsync();
        var listTask = service.GetAppointmentsAsync(new AppointmentSearchRequest());
        var itemTask = service.GetAppointmentAsync(id);
        await Task.WhenAll(listTask, itemTask);
        var appointments = await listTask;
        var appointment = await itemTask;

        Assert.True(factory.CreatedContextCount >= 2);
        Assert.Single(appointments);
        Assert.Equal(id, appointment.Id);
    }

    [Fact]
    public async Task ConcurrentShellInventoryAndScheduleLoads_DoNotThrowSecondOperationException()
    {
        var businessId = Guid.NewGuid();
        var tenantContext = MockTenant(businessId);
        var options = CreateOptions();
        await SeedInventoryAsync(options, tenantContext.Object, businessId, "ITEM");
        await SeedAppointmentAsync(options, tenantContext.Object, businessId);
        var factory = new TestApplicationDbContextFactory(options, tenantContext.Object);
        var businessService = new BusinessService(factory, tenantContext.Object);
        var inventoryService = new InventoryService(factory, tenantContext.Object);
        var appointmentService = new AppointmentService(factory, tenantContext.Object);

        var exception = await Record.ExceptionAsync(async () =>
            await Task.WhenAll(
                businessService.GetCurrentBusinessAsync(),
                inventoryService.GetInventoryProfilesAsync(),
                appointmentService.GetAppointmentsAsync(new AppointmentSearchRequest())));

        Assert.Null(exception);
        Assert.True(factory.CreatedContextCount >= 3);
    }

    private static DbContextOptions<ApplicationDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    private static Mock<ITenantContext> MockTenant(Guid businessId)
    {
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.CurrentBusinessId).Returns(businessId);
        tenantContext.Setup(t => t.UserId).Returns("user-id");
        return tenantContext;
    }

    private static async Task SeedInventoryAsync(
        DbContextOptions<ApplicationDbContext> options,
        ITenantContext tenantContext,
        Guid businessId,
        string itemCode)
    {
        await using var context = new ApplicationDbContext(options, tenantContext);
        context.Businesses.Add(new Business { Id = businessId, Name = $"Business {itemCode}" });
        var item = new CatalogItem
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            ItemCode = itemCode,
            Name = $"Product {itemCode}",
            Type = CatalogItemType.Product,
            Unit = "Ea"
        };
        context.CatalogItems.Add(item);
        context.InventoryProfiles.Add(new InventoryProfile
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CatalogItemId = item.Id,
            IsActive = true
        });
        context.InventoryLocations.Add(new InventoryLocation
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Name = $"Main {itemCode}",
            IsActive = true
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedAppointmentAsync(
        DbContextOptions<ApplicationDbContext> options,
        ITenantContext tenantContext,
        Guid businessId)
    {
        await using var context = new ApplicationDbContext(options, tenantContext);
        if (!await context.Businesses.IgnoreQueryFilters().AnyAsync(b => b.Id == businessId))
        {
            context.Businesses.Add(new Business { Id = businessId, Name = "Business" });
        }

        var job = new Job
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CustomerId = Guid.NewGuid(),
            JobNumber = "JOB-001",
            Title = "Test job",
            Status = JobStatus.Ready
        };
        context.Jobs.Add(job);
        context.Appointments.Add(new Appointment
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            JobId = job.Id,
            Start = DateTimeOffset.UtcNow,
            End = DateTimeOffset.UtcNow.AddHours(1),
            Status = AppointmentStatus.Scheduled
        });
        await context.SaveChangesAsync();
    }
}
