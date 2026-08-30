using Microsoft.EntityFrameworkCore;
using Moq;
using SmallBusiness.Application.DTOs.Quotes;
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

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Name = "Test Cust",
            CustomerNumber = "C-1",
            Email = "original@example.com",
            PhoneNumber = "555-0100"
        };
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CustomerId = customer.Id,
            CustomerNumberSnapshot = customer.CustomerNumber,
            CustomerNameSnapshot = customer.Name,
            CustomerEmailSnapshot = customer.Email,
            CustomerPhoneSnapshot = customer.PhoneNumber,
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
        dbContext.ChangeTracker.Clear();

        var service = new SalesOrderService(dbContext, mockContext.Object, mockSeq.Object);

        // Act
        var soId = await service.ConvertQuoteToSalesOrderAsync(quote.Id);

        // Assert
        var so = await dbContext.SalesOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == soId);
        Assert.NotNull(so);
        Assert.Equal(SalesOrderStatus.Confirmed, so.Status);
        Assert.Equal(quote.Id, so.QuoteId);
        Assert.Equal("C-1", so.CustomerNumberSnapshot);
        Assert.Equal("Test Cust", so.CustomerNameSnapshot);
        Assert.Equal("original@example.com", so.CustomerEmailSnapshot);
        Assert.Equal("555-0100", so.CustomerPhoneSnapshot);
        Assert.Equal(100m, so.Subtotal);
        Assert.Equal(5m, so.DiscountAmount);
        Assert.Equal(10m, so.TaxAmount);
        Assert.Equal(105m, so.Total);
        Assert.Single(so.Lines);
        
        var activity = await dbContext.Activities.FirstOrDefaultAsync(a => a.EntityId == quote.Id);
        Assert.NotNull(activity);
    }

    [Fact]
    public async Task ConvertQuoteToSalesOrderAsync_CustomerChangesAfterQuoteCreation_UsesQuoteSnapshots()
    {
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        mockContext.Setup(x => x.UserId).Returns("user1");

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var quoteSeq = new Mock<ITenantSequenceService>();
        quoteSeq.Setup(x => x.GetNextQuoteNumberAsync()).ReturnsAsync("Q-000001");
        var orderSeq = new Mock<ITenantSequenceService>();
        orderSeq.Setup(x => x.GetNextSalesOrderNumberAsync()).ReturnsAsync("SO-000001");

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CustomerNumber = "CUST-ORIGINAL",
            Name = "Original Customer",
            Email = "original@example.com",
            PhoneNumber = "555-0101",
            AddressStreet = "1 Original St",
            AddressCity = "Original City",
            AddressState = "OS",
            AddressPostalCode = "11111"
        };

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var quoteService = new QuoteService(dbContext, mockContext.Object, quoteSeq.Object);
        var quoteId = await quoteService.CreateQuoteAsync(new CreateQuoteRequest
        {
            CustomerId = customer.Id,
            QuoteDate = DateTime.Today,
            TaxRate = 10m,
            DiscountAmount = 5m,
            Lines =
            [
                new QuoteLineRequest
                {
                    Name = "Quoted Work",
                    Unit = "Each",
                    Quantity = 2m,
                    UnitPrice = 50m,
                    Taxable = true
                }
            ]
        });

        await quoteService.ChangeQuoteStatusAsync(quoteId, QuoteStatus.Sent);
        await quoteService.ChangeQuoteStatusAsync(quoteId, QuoteStatus.Accepted);

        customer.CustomerNumber = "CUST-CHANGED";
        customer.Name = "Changed Customer";
        customer.Email = "changed@example.com";
        customer.PhoneNumber = "555-9999";
        customer.AddressStreet = "99 Changed St";
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var salesOrderService = new SalesOrderService(dbContext, mockContext.Object, orderSeq.Object);
        var salesOrderId = await salesOrderService.ConvertQuoteToSalesOrderAsync(quoteId);

        var quote = await dbContext.Quotes.AsNoTracking().SingleAsync(q => q.Id == quoteId);
        var salesOrder = await dbContext.SalesOrders.Include(o => o.Lines).SingleAsync(o => o.Id == salesOrderId);

        Assert.Equal("CUST-ORIGINAL", quote.CustomerNumberSnapshot);
        Assert.Equal("Original Customer", quote.CustomerNameSnapshot);
        Assert.Equal("original@example.com", quote.CustomerEmailSnapshot);
        Assert.Equal("555-0101", quote.CustomerPhoneSnapshot);
        Assert.Equal(quote.CustomerNumberSnapshot, salesOrder.CustomerNumberSnapshot);
        Assert.Equal(quote.CustomerNameSnapshot, salesOrder.CustomerNameSnapshot);
        Assert.Equal(quote.CustomerEmailSnapshot, salesOrder.CustomerEmailSnapshot);
        Assert.Equal(quote.CustomerPhoneSnapshot, salesOrder.CustomerPhoneSnapshot);
        Assert.Equal(quote.Subtotal, salesOrder.Subtotal);
        Assert.Equal(quote.DiscountAmount, salesOrder.DiscountAmount);
        Assert.Equal(quote.TaxAmount, salesOrder.TaxAmount);
        Assert.Equal(quote.Total, salesOrder.Total);
        Assert.Equal(SalesOrderStatus.Confirmed, salesOrder.Status);
        Assert.Single(salesOrder.Lines);
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
        var quote = new Quote { Id = Guid.NewGuid(), BusinessId = businessId, CustomerId = customer.Id, Status = QuoteStatus.Draft };
        
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
        
        var customer = new Customer { Id = Guid.NewGuid(), BusinessId = businessId, Name = "Test", CustomerNumber = "C-1" };
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CustomerId = customer.Id,
            CustomerNumberSnapshot = customer.CustomerNumber,
            CustomerNameSnapshot = customer.Name,
            Status = QuoteStatus.Accepted
        };
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
    public async Task ConvertQuoteToSalesOrderAsync_CrossTenantQuote_ThrowsKeyNotFound()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(tenantA);

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockSeq = new Mock<ITenantSequenceService>();
        var customer = new Customer { Id = Guid.NewGuid(), BusinessId = tenantB, Name = "Tenant B", CustomerNumber = "B-1" };
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            BusinessId = tenantB,
            CustomerId = customer.Id,
            CustomerNumberSnapshot = customer.CustomerNumber,
            CustomerNameSnapshot = customer.Name,
            QuoteNumber = "QB-1",
            Status = QuoteStatus.Accepted
        };

        dbContext.Customers.Add(customer);
        dbContext.Quotes.Add(quote);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var service = new SalesOrderService(dbContext, mockContext.Object, mockSeq.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ConvertQuoteToSalesOrderAsync(quote.Id));
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
