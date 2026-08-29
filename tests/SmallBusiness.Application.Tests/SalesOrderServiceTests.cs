using Microsoft.EntityFrameworkCore;
using Moq;
using SmallBusiness.Application.DTOs.SalesOrders;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Application.Services;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;
using SmallBusiness.Infrastructure.Data;
using System.ComponentModel.DataAnnotations;

namespace SmallBusiness.Application.Tests;

public class SalesOrderServiceTests
{
    private static ApplicationDbContext CreateInMemoryContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, tenantContext);
    }

    [Fact]
    public async Task ConvertQuoteToSalesOrderAsync_ValidAcceptedQuote_CalculatesCorrectlyAndStartsConfirmed()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        mockContext.Setup(x => x.UserId).Returns("user1");

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockSeq = new Mock<ITenantSequenceService>();
        mockSeq.Setup(x => x.GetNextSalesOrderNumberAsync()).ReturnsAsync("SO-000001");

        var customer = new Customer { Id = Guid.NewGuid(), BusinessId = businessId, Name = "Test Cust", CustomerNumber = "C-1" };
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CustomerId = customer.Id,
            Customer = customer,
            CustomerNameSnapshot = customer.Name,
            QuoteNumber = "Q-1",
            Status = QuoteStatus.Accepted,
            TaxRate = 10m,
            DiscountAmount = 5m,
            Lines = new List<QuoteLine>
            {
                new QuoteLine { UnitPrice = 100m, Quantity = 1m, Taxable = true, LineTotal = 100m }
            },
            Subtotal = 100m,
            TaxAmount = 10m,
            Total = 105m // 100 - 5 + 10
        };

        dbContext.Customers.Add(customer);
        dbContext.Quotes.Add(quote);
        await dbContext.SaveChangesAsync();

        var service = new SalesOrderService(dbContext, mockContext.Object, mockSeq.Object);

        // Act
        var soId = await service.ConvertQuoteToSalesOrderAsync(quote.Id);

        // Assert
        var so = await dbContext.SalesOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == soId);
        Assert.NotNull(so);
        Assert.Equal(SalesOrderStatus.Confirmed, so.Status);
        Assert.Equal(quote.Id, so.QuoteId);
        Assert.Equal(100m, so.Subtotal);
        Assert.Equal(5m, so.DiscountAmount);
        Assert.Equal(10m, so.TaxAmount);
        Assert.Equal(105m, so.Total);
        Assert.Single(so.Lines);
        
        var activity = await dbContext.Activities.FirstOrDefaultAsync(a => a.EntityId == quote.Id);
        Assert.NotNull(activity);
    }

    [Fact]
    public async Task ConvertQuoteToSalesOrderAsync_DraftQuote_ThrowsException()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockSeq = new Mock<ITenantSequenceService>();
        
        var customer = new Customer { Id = Guid.NewGuid(), BusinessId = businessId, Name = "Test" };
        var quote = new Quote { Id = Guid.NewGuid(), BusinessId = businessId, CustomerId = customer.Id, Customer = customer, Status = QuoteStatus.Draft };
        
        dbContext.Customers.Add(customer);
        dbContext.Quotes.Add(quote);
        await dbContext.SaveChangesAsync();

        var service = new SalesOrderService(dbContext, mockContext.Object, mockSeq.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.ConvertQuoteToSalesOrderAsync(quote.Id));
    }

    [Fact]
    public async Task ConvertQuoteToSalesOrderAsync_AlreadyConverted_ThrowsException()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockSeq = new Mock<ITenantSequenceService>();
        
        var customer = new Customer { Id = Guid.NewGuid(), BusinessId = businessId, Name = "Test" };
        var quote = new Quote { Id = Guid.NewGuid(), BusinessId = businessId, CustomerId = customer.Id, Customer = customer, Status = QuoteStatus.Accepted };
        var existingSo = new SalesOrder { Id = Guid.NewGuid(), BusinessId = businessId, QuoteId = quote.Id };

        dbContext.Customers.Add(customer);
        dbContext.Quotes.Add(quote);
        dbContext.SalesOrders.Add(existingSo);
        await dbContext.SaveChangesAsync();

        var service = new SalesOrderService(dbContext, mockContext.Object, mockSeq.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.ConvertQuoteToSalesOrderAsync(quote.Id));
    }
    
    [Fact]
    public async Task CreateSalesOrderAsync_ValidRequest_StartsDraftAndCalculates()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockSeq = new Mock<ITenantSequenceService>();
        mockSeq.Setup(x => x.GetNextSalesOrderNumberAsync()).ReturnsAsync("SO-000002");
        
        var customer = new Customer { Id = Guid.NewGuid(), BusinessId = businessId, Name = "Test" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var service = new SalesOrderService(dbContext, mockContext.Object, mockSeq.Object);
        
        var req = new CreateSalesOrderRequest
        {
            CustomerId = customer.Id,
            TaxRate = 5m,
            Lines = new List<SalesOrderLineRequest>
            {
                new SalesOrderLineRequest { Name = "Item", Unit = "Hr", Quantity = 2m, UnitPrice = 50m, Taxable = true }
            }
        };

        // Act
        var id = await service.CreateSalesOrderAsync(req);
        
        // Assert
        var so = await dbContext.SalesOrders.FindAsync(id);
        Assert.Equal(SalesOrderStatus.Draft, so!.Status);
        Assert.Equal(100m, so.Subtotal);
        Assert.Equal(5m, so.TaxAmount);
        Assert.Equal(105m, so.Total);
    }
}
