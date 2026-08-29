using Microsoft.EntityFrameworkCore;
using Moq;
using SmallBusiness.Application.DTOs.Quotes;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Application.Services;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;
using SmallBusiness.Infrastructure.Data;
using System.ComponentModel.DataAnnotations;

namespace SmallBusiness.Application.Tests;

public class QuoteServiceTests
{
    private static ApplicationDbContext CreateInMemoryContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, tenantContext);
    }

    [Fact]
    public async Task CreateQuoteAsync_ValidRequest_CalculatesTotalsAndSetsStatusToDraft()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        mockContext.Setup(x => x.UserId).Returns("user1");

        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockSequenceService = new Mock<ITenantSequenceService>();
        mockSequenceService.Setup(x => x.GetNextQuoteNumberAsync()).ReturnsAsync("QUOTE-000001");

        var customer = new Customer { Id = Guid.NewGuid(), BusinessId = businessId, Name = "Test Customer" };
        dbContext.Customers.Add(customer);
        
        var catalogItem = new CatalogItem { Id = Guid.NewGuid(), BusinessId = businessId, Name = "Test Item", SellingPrice = 100m, Taxable = true };
        dbContext.CatalogItems.Add(catalogItem);
        
        await dbContext.SaveChangesAsync();

        var service = new QuoteService(dbContext, mockContext.Object, mockSequenceService.Object);

        var request = new CreateQuoteRequest
        {
            CustomerId = customer.Id,
            QuoteDate = DateTime.Today,
            TaxRate = 10m,
            DiscountAmount = 15m,
            Lines = new List<QuoteLineRequest>
            {
                new QuoteLineRequest
                {
                    CatalogItemId = catalogItem.Id,
                    Quantity = 2m
                },
                new QuoteLineRequest
                {
                    Name = "Manual Item",
                    Unit = "Hour",
                    Quantity = 1.5m,
                    UnitPrice = 50m,
                    Taxable = false
                }
            }
        };

        // Act
        var quoteId = await service.CreateQuoteAsync(request);

        // Assert
        var quote = await dbContext.Quotes.Include(q => q.Lines).FirstOrDefaultAsync(q => q.Id == quoteId);
        Assert.NotNull(quote);
        Assert.Equal(QuoteStatus.Draft, quote.Status);
        Assert.Equal(businessId, quote.BusinessId);
        Assert.Equal("QUOTE-000001", quote.QuoteNumber);
        
        // Math check
        // Line 1: 2 * 100 = 200 (Taxable)
        // Line 2: 1.5 * 50 = 75 (Non-taxable)
        // Subtotal = 275
        // Discount = 15
        // TaxableAmount = 200 -> Tax = 200 * 0.10 = 20
        // Total = 275 - 15 + 20 = 280
        
        Assert.Equal(275m, quote.Subtotal);
        Assert.Equal(20m, quote.TaxAmount);
        Assert.Equal(280m, quote.Total);
        
        Assert.Equal(2, quote.Lines.Count);
        
        var activity = await dbContext.Activities.FirstOrDefaultAsync(a => a.EntityId == quoteId);
        Assert.NotNull(activity);
        Assert.Equal(ActivityType.Created, activity.ActivityType);
    }

    [Fact]
    public async Task CreateQuoteAsync_DiscountExceedsSubtotal_ThrowsValidationException()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        
        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockSeq = new Mock<ITenantSequenceService>();
        var service = new QuoteService(dbContext, mockContext.Object, mockSeq.Object);

        var customer = new Customer { Id = Guid.NewGuid(), BusinessId = businessId, Name = "Test" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var request = new CreateQuoteRequest
        {
            CustomerId = customer.Id,
            DiscountAmount = 200m,
            Lines = new List<QuoteLineRequest>
            {
                new QuoteLineRequest { Name = "Item", Unit = "Ea", Quantity = 1, UnitPrice = 100m }
            }
        }; // Subtotal is 100, discount is 200

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateQuoteAsync(request));
    }

    [Fact]
    public async Task UpdateDraftQuoteAsync_OnlyDraftCanBeEdited()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        
        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockSeq = new Mock<ITenantSequenceService>();
        var service = new QuoteService(dbContext, mockContext.Object, mockSeq.Object);

        var customer = new Customer { Id = Guid.NewGuid(), BusinessId = businessId, Name = "Test" };
        var quote = new Quote { Id = Guid.NewGuid(), BusinessId = businessId, Status = QuoteStatus.Sent };
        dbContext.Customers.Add(customer);
        dbContext.Quotes.Add(quote);
        await dbContext.SaveChangesAsync();

        var request = new UpdateQuoteRequest
        {
            CustomerId = customer.Id,
            Lines = new List<QuoteLineRequest> { new QuoteLineRequest { Name = "Item", Unit = "Ea", Quantity = 1, UnitPrice = 10 } }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateDraftQuoteAsync(quote.Id, request));
    }

    [Fact]
    public async Task ChangeQuoteStatusAsync_ValidTransitions_Succeeds()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        
        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockSeq = new Mock<ITenantSequenceService>();
        var service = new QuoteService(dbContext, mockContext.Object, mockSeq.Object);

        var quote = new Quote 
        { 
            Id = Guid.NewGuid(), 
            BusinessId = businessId, 
            Status = QuoteStatus.Draft,
            Lines = new List<QuoteLine> { new QuoteLine { Name = "Item" } }
        };
        dbContext.Quotes.Add(quote);
        await dbContext.SaveChangesAsync();

        // Act - Draft -> Sent
        await service.ChangeQuoteStatusAsync(quote.Id, QuoteStatus.Sent);
        
        // Assert
        var updated = await dbContext.Quotes.FindAsync(quote.Id);
        Assert.Equal(QuoteStatus.Sent, updated!.Status);
        Assert.NotNull(updated.SentAt);

        // Act - Sent -> Accepted
        await service.ChangeQuoteStatusAsync(quote.Id, QuoteStatus.Accepted);
        
        // Assert
        Assert.Equal(QuoteStatus.Accepted, updated.Status);
        Assert.NotNull(updated.AcceptedAt);
    }
    
    [Fact]
    public async Task ChangeQuoteStatusAsync_InvalidTransition_ThrowsValidationException()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        
        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockSeq = new Mock<ITenantSequenceService>();
        var service = new QuoteService(dbContext, mockContext.Object, mockSeq.Object);

        var quote = new Quote { Id = Guid.NewGuid(), BusinessId = businessId, Status = QuoteStatus.Draft };
        dbContext.Quotes.Add(quote);
        await dbContext.SaveChangesAsync();

        // Act & Assert - Draft -> Accepted is invalid
        await Assert.ThrowsAsync<ValidationException>(() => service.ChangeQuoteStatusAsync(quote.Id, QuoteStatus.Accepted));
    }
    
    [Fact]
    public async Task ChangeQuoteStatusAsync_AcceptingExpiredQuote_ThrowsValidationException()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var mockContext = new Mock<ITenantContext>();
        mockContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        
        await using var dbContext = CreateInMemoryContext(mockContext.Object);
        var mockSeq = new Mock<ITenantSequenceService>();
        var service = new QuoteService(dbContext, mockContext.Object, mockSeq.Object);

        var quote = new Quote 
        { 
            Id = Guid.NewGuid(), 
            BusinessId = businessId, 
            Status = QuoteStatus.Sent,
            ExpirationDate = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.Quotes.Add(quote);
        await dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.ChangeQuoteStatusAsync(quote.Id, QuoteStatus.Accepted));
    }
}
