using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Moq;
using SmallBusiness.Application.DTOs.Invoices;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Application.Services;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;
using SmallBusiness.Infrastructure.Data;
using Xunit;

namespace SmallBusiness.Application.Tests;

public class InvoiceServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ITenantSequenceService> _mockSequenceService;
    private readonly InvoiceService _service;
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public InvoiceServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(t => t.CurrentBusinessId).Returns(_businessId);
        _mockTenantContext.Setup(t => t.UserId).Returns(_userId.ToString());
            
        _context = new ApplicationDbContext(options, _mockTenantContext.Object);

        _mockSequenceService = new Mock<ITenantSequenceService>();
        _mockSequenceService.Setup(s => s.GetNextInvoiceNumberAsync()).ReturnsAsync("INV-000001");

        _service = new InvoiceService(_context, _mockTenantContext.Object, _mockSequenceService.Object);
    }

    [Fact]
    public async Task CreateInvoiceFromSalesOrderAsync_CompletedOrder_CreatesInvoice()
    {
        // Arrange
        var customer = new Customer { Id = Guid.NewGuid(), BusinessId = _businessId, CustomerNumber = "CUST-1", Name = "John Doe" };
        _context.Customers.Add(customer);
        
        var order = new SalesOrder
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            CustomerId = customer.Id,
            CustomerNameSnapshot = "John Doe",
            Status = SalesOrderStatus.Completed,
            TaxRate = 10m,
            Subtotal = 100m,
            TaxAmount = 10m,
            Total = 110m,
            Lines = new List<SalesOrderLine>
            {
                new SalesOrderLine { Id = Guid.NewGuid(), ItemCode = "ITEM-1", Quantity = 1, UnitPrice = 100m, Taxable = true, LineTotal = 100m }
            }
        };
        _context.SalesOrders.Add(order);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CreateInvoiceFromSalesOrderAsync(order.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("INV-000001", result.InvoiceNumber);
        Assert.Equal(InvoiceStatus.Draft, result.Status);
        Assert.Equal("John Doe", result.CustomerName);
        Assert.Equal(110m, result.Total);
        Assert.Equal(110m, result.BalanceDue);
        Assert.Single(result.Lines);
        
        var activityCount = await _context.Activities.CountAsync();
        Assert.Equal(2, activityCount); // one for SO, one for Invoice
    }

    [Fact]
    public async Task CreateInvoiceFromSalesOrderAsync_NotCompletedOrder_ThrowsValidationException()
    {
        // Arrange
        var order = new SalesOrder
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            Status = SalesOrderStatus.Confirmed
        };
        _context.SalesOrders.Add(order);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateInvoiceFromSalesOrderAsync(order.Id));
    }

    [Fact]
    public async Task CreateInvoiceFromSalesOrderAsync_DuplicateActiveInvoice_ThrowsValidationException()
    {
        // Arrange
        var order = new SalesOrder
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            Status = SalesOrderStatus.Completed,
            Subtotal = 100, Total = 100
        };
        _context.SalesOrders.Add(order);
        
        var existingInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            SalesOrderId = order.Id,
            Status = InvoiceStatus.Sent,
            InvoiceNumber = "INV-000001",
            Subtotal = 100, Total = 100
        };
        _context.Invoices.Add(existingInvoice);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateInvoiceFromSalesOrderAsync(order.Id));
    }
    
    [Fact]
    public async Task CreateInvoiceFromSalesOrderAsync_AfterVoidInvoice_CreatesReplacement()
    {
        // Arrange
        var order = new SalesOrder
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            Status = SalesOrderStatus.Completed,
            Subtotal = 0, Total = 0
        };
        _context.SalesOrders.Add(order);
        
        var voidInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            SalesOrderId = order.Id,
            Status = InvoiceStatus.Void,
            InvoiceNumber = "INV-VOID",
            Subtotal = 0, Total = 0
        };
        _context.Invoices.Add(voidInvoice);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CreateInvoiceFromSalesOrderAsync(order.Id);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpdateDraftInvoiceMetadataAsync_UpdatesFieldsAndDueDateValidates()
    {
        // Arrange
        var so = new SalesOrder { Id = Guid.NewGuid(), BusinessId = _businessId, Status = SalesOrderStatus.Completed };
        _context.SalesOrders.Add(so);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            SalesOrderId = so.Id,
            Status = InvoiceStatus.Draft,
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15))
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var newDueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var req = new UpdateInvoiceMetadataRequest
        {
            Id = invoice.Id,
            DueDate = newDueDate,
            Notes = "Updated Notes",
            Terms = "Net 30"
        };

        // Act
        var result = await _service.UpdateDraftInvoiceMetadataAsync(req);

        // Assert
        Assert.Equal(newDueDate, result.DueDate);
        Assert.Equal("Updated Notes", result.Notes);
        Assert.Equal("Net 30", result.Terms);
    }
    
    [Fact]
    public async Task UpdateDraftInvoiceMetadataAsync_InvalidDueDate_Throws()
    {
        // Arrange
        var so = new SalesOrder { Id = Guid.NewGuid(), BusinessId = _businessId, Status = SalesOrderStatus.Completed };
        _context.SalesOrders.Add(so);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            SalesOrderId = so.Id,
            Status = InvoiceStatus.Draft,
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var req = new UpdateInvoiceMetadataRequest
        {
            Id = invoice.Id,
            DueDate = invoice.InvoiceDate.AddDays(-1)
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateDraftInvoiceMetadataAsync(req));
    }

    [Fact]
    public async Task SendInvoiceAsync_ChangesStatusToSent()
    {
        // Arrange
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            Status = InvoiceStatus.Draft
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        // Act
        await _service.SendInvoiceAsync(invoice.Id);

        // Assert
        var updated = await _context.Invoices.FindAsync(invoice.Id);
        Assert.Equal(InvoiceStatus.Sent, updated!.Status);
        Assert.NotNull(updated.SentAt);
    }

    [Fact]
    public async Task VoidInvoiceAsync_CreatesAuditLogAndChangesStatus()
    {
        // Arrange
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            Status = InvoiceStatus.Sent
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        // Act
        await _service.VoidInvoiceAsync(invoice.Id);

        // Assert
        var updated = await _context.Invoices.FindAsync(invoice.Id);
        Assert.Equal(InvoiceStatus.Void, updated!.Status);
        Assert.NotNull(updated.VoidedAt);

        var audit = await _context.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == invoice.Id);
        Assert.NotNull(audit);
        Assert.Equal("Void Invoice", audit.Action);
    }

    [Fact]
    public async Task GetInvoicesAsync_OverdueOnly_FiltersCorrectly()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var so = new SalesOrder { Id = Guid.NewGuid(), BusinessId = _businessId, Status = SalesOrderStatus.Completed };
        _context.SalesOrders.Add(so);
        
        var overdue = new Invoice
        {
            Id = Guid.NewGuid(), BusinessId = _businessId, SalesOrderId = so.Id, Status = InvoiceStatus.Sent,
            InvoiceDate = today.AddDays(-30), DueDate = today.AddDays(-1),
            Total = 100, BalanceDue = 100
        };
        var notOverdue = new Invoice
        {
            Id = Guid.NewGuid(), BusinessId = _businessId, SalesOrderId = so.Id, Status = InvoiceStatus.Sent,
            InvoiceDate = today, DueDate = today.AddDays(15),
            Total = 100, BalanceDue = 100
        };
        
        _context.Invoices.AddRange(overdue, notOverdue);
        await _context.SaveChangesAsync();

        // Act
        var req = new InvoiceSearchRequest { OverdueOnly = true };
        var results = await _service.GetInvoicesAsync(req);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].IsOverdue);
        Assert.Equal(overdue.Id, results[0].Id);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
