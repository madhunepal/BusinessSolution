using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Moq;
using SmallBusiness.Application.DTOs.Payments;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Application.Services;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;
using SmallBusiness.Infrastructure.Data;
using Xunit;

namespace SmallBusiness.Application.Tests;

public class PaymentServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ITenantSequenceService> _mockSequenceService;
    private readonly PaymentService _service;
    private readonly Guid _businessId = Guid.NewGuid();

    public PaymentServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(t => t.CurrentBusinessId).Returns(_businessId);
        _mockTenantContext.Setup(t => t.UserId).Returns("test-user");

        _context = new ApplicationDbContext(options, _mockTenantContext.Object);

        _mockSequenceService = new Mock<ITenantSequenceService>();
        _mockSequenceService.Setup(s => s.GetNextPaymentNumberAsync()).ReturnsAsync("PAY-000001");

        _service = new PaymentService(_context, _mockTenantContext.Object, _mockSequenceService.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task CreatePayment_ValidPayment_Succeeds()
    {
        // Seed data
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            InvoiceNumber = "INV-001",
            Status = InvoiceStatus.Sent,
            Total = 100,
            AmountPaid = 0,
            BalanceDue = 100,
            RowVersion = new byte[8],
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var request = new CreatePaymentDto
        {
            InvoiceId = invoice.Id,
            Amount = 40,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Method = PaymentMethod.CreditCard,
            ReferenceNumber = "TX123"
        };

        // Act
        var result = await _service.CreatePaymentAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(40, result.Amount);
        Assert.Equal(PaymentMethod.CreditCard, result.Method);

        var updatedInvoice = await _context.Invoices.FindAsync(invoice.Id);
        Assert.Equal(40, updatedInvoice!.AmountPaid);
        Assert.Equal(60, updatedInvoice.BalanceDue);
        Assert.Equal(InvoiceStatus.PartiallyPaid, updatedInvoice.Status);
        Assert.Null(updatedInvoice.PaidAt);
    }

    [Fact]
    public async Task CreatePayment_FullPayment_CompletesInvoice()
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            InvoiceNumber = "INV-002",
            Status = InvoiceStatus.PartiallyPaid,
            Total = 100,
            AmountPaid = 40,
            BalanceDue = 60,
            RowVersion = new byte[8]
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var request = new CreatePaymentDto
        {
            InvoiceId = invoice.Id,
            Amount = 60,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Method = PaymentMethod.Check
        };

        // Act
        var result = await _service.CreatePaymentAsync(request);

        // Assert
        var updatedInvoice = await _context.Invoices.FindAsync(invoice.Id);
        Assert.Equal(100, updatedInvoice!.AmountPaid);
        Assert.Equal(0, updatedInvoice.BalanceDue);
        Assert.Equal(InvoiceStatus.Paid, updatedInvoice.Status);
        Assert.NotNull(updatedInvoice.PaidAt);
    }

    [Fact]
    public async Task CreatePayment_Overpayment_ThrowsValidationException()
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            InvoiceNumber = "INV-003",
            Status = InvoiceStatus.Sent,
            Total = 100,
            BalanceDue = 100,
            RowVersion = new byte[8]
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var request = new CreatePaymentDto
        {
            InvoiceId = invoice.Id,
            Amount = 101, // Overpayment
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Method = PaymentMethod.Check
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _service.CreatePaymentAsync(request));
    }

    [Fact]
    public async Task CreatePayment_ZeroOrNegative_ThrowsValidationException()
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            InvoiceNumber = "INV-004",
            Status = InvoiceStatus.Sent,
            Total = 100,
            BalanceDue = 100,
            RowVersion = new byte[8]
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var request = new CreatePaymentDto
        {
            InvoiceId = invoice.Id,
            Amount = 0,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Method = PaymentMethod.Check
        };

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreatePaymentAsync(request));
        
        request.Amount = -10;
        await Assert.ThrowsAsync<ValidationException>(() => _service.CreatePaymentAsync(request));
    }

    [Fact]
    public async Task CreatePayment_DraftInvoice_ThrowsValidationException()
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            InvoiceNumber = "INV-005",
            Status = InvoiceStatus.Draft, // Draft
            Total = 100,
            BalanceDue = 100,
            RowVersion = new byte[8]
        };
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var request = new CreatePaymentDto { InvoiceId = invoice.Id, Amount = 10, PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow), Method = PaymentMethod.Check };

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreatePaymentAsync(request));
    }
}
