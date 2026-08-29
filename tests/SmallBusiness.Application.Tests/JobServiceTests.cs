using Microsoft.EntityFrameworkCore;
using Moq;
using SmallBusiness.Application.DTOs.Jobs;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Application.Services;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;
using SmallBusiness.Infrastructure.Data;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace SmallBusiness.Application.Tests;

public class JobServiceTests
{
    private ApplicationDbContext CreateInMemoryContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        return new ApplicationDbContext(options, tenantContext);
    }

    [Fact]
    public async Task CreateJobFromSalesOrderAsync_ValidOrder_CreatesJobAndTasks()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(c => c.CurrentBusinessId).Returns(businessId);
        mockContext.Setup(c => c.UserId).Returns("test-user");

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockSeq = new Mock<ITenantSequenceService>();
        mockSeq.Setup(x => x.GetNextJobNumberAsync()).ReturnsAsync("JOB-000001");
        
        var customer = new Customer { Id = Guid.NewGuid(), BusinessId = businessId, Name = "Test Customer" };
        dbContext.Customers.Add(customer);
        
        var salesOrder = new SalesOrder
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CustomerId = customer.Id,
            SalesOrderNumber = "SO-000001",
            Status = SalesOrderStatus.Confirmed,
            Lines = new List<SalesOrderLine>
            {
                new SalesOrderLine { Id = Guid.NewGuid(), Name = "Install A", Quantity = 1 },
                new SalesOrderLine { Id = Guid.NewGuid(), Name = "Install B", Quantity = 2 }
            }
        };
        dbContext.SalesOrders.Add(salesOrder);
        await dbContext.SaveChangesAsync();

        var service = new JobService(dbContext, mockContext.Object, mockSeq.Object);

        // Act
        var jobId = await service.CreateJobFromSalesOrderAsync(salesOrder.Id);

        // Assert
        var job = await dbContext.Jobs.Include(j => j.Tasks).FirstOrDefaultAsync(j => j.Id == jobId);
        Assert.NotNull(job);
        Assert.Equal("JOB-000001", job.JobNumber);
        Assert.Equal(JobStatus.Draft, job.Status);
        Assert.Equal(salesOrder.Id, job.SalesOrderId);
        Assert.Equal(2, job.Tasks.Count);
        
        // Assert Activity
        var activityCount = await dbContext.Activities.CountAsync(a => a.EntityId == jobId);
        Assert.Equal(1, activityCount);
    }

    [Fact]
    public async Task CreateJobFromSalesOrderAsync_DuplicateActiveJob_ThrowsValidationException()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(c => c.CurrentBusinessId).Returns(businessId);

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockSeq = new Mock<ITenantSequenceService>();
        
        var customer = new Customer { Id = Guid.NewGuid(), BusinessId = businessId, Name = "Test" };
        dbContext.Customers.Add(customer);
        
        var salesOrder = new SalesOrder
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CustomerId = customer.Id,
            SalesOrderNumber = "SO-000001",
            Status = SalesOrderStatus.Confirmed
        };
        dbContext.SalesOrders.Add(salesOrder);
        
        var existingJob = new Job
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            SalesOrderId = salesOrder.Id,
            Status = JobStatus.Draft,
            CustomerId = salesOrder.CustomerId
        };
        dbContext.Jobs.Add(existingJob);
        await dbContext.SaveChangesAsync();

        var service = new JobService(dbContext, mockContext.Object, mockSeq.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateJobFromSalesOrderAsync(salesOrder.Id));
    }
    
    [Fact]
    public async Task ChangeJobStatusAsync_ToCompleted_AutoCompletesSalesOrder()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(c => c.CurrentBusinessId).Returns(businessId);

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockSeq = new Mock<ITenantSequenceService>();
        
        var customer = new Customer { Id = Guid.NewGuid(), BusinessId = businessId, Name = "Test" };
        dbContext.Customers.Add(customer);
        
        var salesOrder = new SalesOrder
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CustomerId = customer.Id,
            SalesOrderNumber = "SO-000001",
            Status = SalesOrderStatus.Confirmed
        };
        dbContext.SalesOrders.Add(salesOrder);
        
        var job = new Job
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            SalesOrderId = salesOrder.Id,
            Status = JobStatus.InProgress,
            CustomerId = salesOrder.CustomerId
        };
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        var service = new JobService(dbContext, mockContext.Object, mockSeq.Object);

        // Act
        await service.ChangeJobStatusAsync(job.Id, JobStatus.Completed, "Done");

        // Assert
        var updatedJob = await dbContext.Jobs.FindAsync(job.Id);
        Assert.Equal(JobStatus.Completed, updatedJob!.Status);
        
        var updatedSO = await dbContext.SalesOrders.FindAsync(salesOrder.Id);
        Assert.Equal(SalesOrderStatus.Completed, updatedSO!.Status);
    }
}
