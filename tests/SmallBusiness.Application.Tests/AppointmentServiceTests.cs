using Microsoft.EntityFrameworkCore;
using Moq;
using SmallBusiness.Application.DTOs.Appointments;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Application.Services;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;
using SmallBusiness.Infrastructure.Data;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace SmallBusiness.Application.Tests;

public class AppointmentServiceTests
{
    private static DbContextOptions<ApplicationDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private ApplicationDbContext CreateInMemoryContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantContext tenantContext)
    {
        return new ApplicationDbContext(options, tenantContext);
    }

    [Fact]
    public async Task CreateAppointmentAsync_ValidRequest_CreatesAppointment()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(c => c.CurrentBusinessId).Returns(businessId);
        mockContext.Setup(c => c.UserId).Returns("test-user");

        var options = CreateInMemoryOptions();
        await using var dbContext = CreateInMemoryContext(options, mockContext.Object);
        
        var job = new Job
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CustomerId = Guid.NewGuid(),
            Status = JobStatus.Ready
        };
        dbContext.Jobs.Add(job);
        
        var user = new BusinessUser
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            UserId = "tech-1",
            IsActive = true
        };
        dbContext.Set<BusinessUser>().Add(user);
        
        await dbContext.SaveChangesAsync();

        var service = new AppointmentService(new TestApplicationDbContextFactory(options, mockContext.Object), mockContext.Object);
        
        var request = new CreateAppointmentRequest
        {
            JobId = job.Id,
            Start = DateTimeOffset.UtcNow.AddDays(1),
            End = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            AssignedUserIds = new List<string> { "tech-1" }
        };

        // Act
        var id = await service.CreateAppointmentAsync(request);

        // Assert
        var appt = await dbContext.Appointments.Include(a => a.Assignments).FirstOrDefaultAsync(a => a.Id == id);
        Assert.NotNull(appt);
        Assert.Equal(AppointmentStatus.Scheduled, appt.Status);
        Assert.Single(appt.Assignments);
        Assert.Equal("tech-1", appt.Assignments.First().UserId);
    }

    [Fact]
    public async Task CreateAppointmentAsync_OverlappingTechnician_ThrowsConflict()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(c => c.CurrentBusinessId).Returns(businessId);

        var options = CreateInMemoryOptions();
        await using var dbContext = CreateInMemoryContext(options, mockContext.Object);
        
        var job = new Job
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CustomerId = Guid.NewGuid(),
            Status = JobStatus.Ready
        };
        dbContext.Jobs.Add(job);
        
        var user = new BusinessUser
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            UserId = "tech-1",
            IsActive = true
        };
        dbContext.Set<BusinessUser>().Add(user);
        
        var existingAppt = new Appointment
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            JobId = job.Id,
            Start = DateTimeOffset.UtcNow.AddDays(1).AddHours(9),
            End = DateTimeOffset.UtcNow.AddDays(1).AddHours(11),
            Status = AppointmentStatus.Scheduled
        };
        existingAppt.Assignments.Add(new AppointmentAssignment
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            UserId = "tech-1",
            AssignedAt = DateTimeOffset.UtcNow
        });
        dbContext.Appointments.Add(existingAppt);
        
        await dbContext.SaveChangesAsync();

        var service = new AppointmentService(new TestApplicationDbContextFactory(options, mockContext.Object), mockContext.Object);
        
        var request = new CreateAppointmentRequest
        {
            JobId = job.Id,
            Start = DateTimeOffset.UtcNow.AddDays(1).AddHours(10), // Overlaps
            End = DateTimeOffset.UtcNow.AddDays(1).AddHours(12),
            AssignedUserIds = new List<string> { "tech-1" }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAppointmentAsync(request));
        Assert.Contains("SCHEDULING_CONFLICT", ex.Message);
        
        // Should succeed if ignoreConflicts is true
        var id = await service.CreateAppointmentAsync(request, ignoreConflicts: true);
        Assert.NotEqual(Guid.Empty, id);
    }
    
    [Fact]
    public async Task ChangeAppointmentStatusAsync_StartAppointment_SetsJobInProgress()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(c => c.CurrentBusinessId).Returns(businessId);

        var options = CreateInMemoryOptions();
        await using var dbContext = CreateInMemoryContext(options, mockContext.Object);
        
        var job = new Job
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CustomerId = Guid.NewGuid(),
            Status = JobStatus.Ready // Important: must be Ready
        };
        dbContext.Jobs.Add(job);
        
        var appt = new Appointment
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            JobId = job.Id,
            Start = DateTimeOffset.UtcNow.AddDays(1),
            End = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            Status = AppointmentStatus.Scheduled
        };
        dbContext.Appointments.Add(appt);
        
        await dbContext.SaveChangesAsync();

        var service = new AppointmentService(new TestApplicationDbContextFactory(options, mockContext.Object), mockContext.Object);

        // Act
        await service.ChangeAppointmentStatusAsync(appt.Id, AppointmentStatus.InProgress);

        // Assert
        dbContext.ChangeTracker.Clear();
        var updatedJob = await dbContext.Jobs.FindAsync(job.Id);
        Assert.Equal(JobStatus.InProgress, updatedJob!.Status);
        
        var updatedAppt = await dbContext.Appointments.FindAsync(appt.Id);
        Assert.Equal(AppointmentStatus.InProgress, updatedAppt!.Status);
        Assert.NotNull(updatedAppt.ActualStart);
    }
    
    [Fact]
    public async Task ChangeAppointmentStatusAsync_CompleteAppointment_DoesNotCompleteJob()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(c => c.CurrentBusinessId).Returns(businessId);

        var options = CreateInMemoryOptions();
        await using var dbContext = CreateInMemoryContext(options, mockContext.Object);
        
        var job = new Job
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CustomerId = Guid.NewGuid(),
            Status = JobStatus.InProgress
        };
        dbContext.Jobs.Add(job);
        
        var appt = new Appointment
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            JobId = job.Id,
            Start = DateTimeOffset.UtcNow.AddDays(-1),
            End = DateTimeOffset.UtcNow.AddDays(-1).AddHours(2),
            Status = AppointmentStatus.InProgress
        };
        dbContext.Appointments.Add(appt);
        
        await dbContext.SaveChangesAsync();

        var service = new AppointmentService(new TestApplicationDbContextFactory(options, mockContext.Object), mockContext.Object);

        // Act
        await service.ChangeAppointmentStatusAsync(appt.Id, AppointmentStatus.Completed);

        // Assert
        dbContext.ChangeTracker.Clear();
        var updatedJob = await dbContext.Jobs.FindAsync(job.Id);
        Assert.Equal(JobStatus.InProgress, updatedJob!.Status); // Job is STILL InProgress
        
        var updatedAppt = await dbContext.Appointments.FindAsync(appt.Id);
        Assert.Equal(AppointmentStatus.Completed, updatedAppt!.Status);
        Assert.NotNull(updatedAppt.CompletedAt);
    }
}
