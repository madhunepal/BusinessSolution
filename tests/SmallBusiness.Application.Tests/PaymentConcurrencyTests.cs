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

public class PaymentConcurrencyTests
{
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Mock<ITenantContext> _tenantContext = new();

    public PaymentConcurrencyTests()
    {
        _tenantContext.Setup(x => x.CurrentBusinessId).Returns(_businessId);
        _tenantContext.Setup(x => x.UserId).Returns("test-user-id");
    }

    [Fact]
    public async Task ConcurrentPayments_ReevaluateBalanceAndDoNotLeaveFailedAttemptSideEffects()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new InMemoryConcurrencyInterceptor())
            .Options;

        Guid invoiceId;
        using (var setupContext = new ApplicationDbContext(options, _tenantContext.Object))
        {
            var seededInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                BusinessId = _businessId,
                InvoiceNumber = "INV-100",
                Status = InvoiceStatus.Sent,
                Total = 100,
                AmountPaid = 0,
                BalanceDue = 100,
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                RowVersion = new byte[8]
            };

            setupContext.Invoices.Add(seededInvoice);
            await setupContext.SaveChangesAsync();
            invoiceId = seededInvoice.Id;
        }

        using var contextA = new ApplicationDbContext(options, _tenantContext.Object);
        using var contextB = new ApplicationDbContext(options, _tenantContext.Object);
        await contextA.Invoices.Include(i => i.Payments).FirstAsync(i => i.Id == invoiceId);
        await contextB.Invoices.Include(i => i.Payments).FirstAsync(i => i.Id == invoiceId);

        var sequenceA = new Mock<ITenantSequenceService>();
        sequenceA.Setup(x => x.GetNextPaymentNumberAsync()).ReturnsAsync("PAY-000001");
        var sequenceB = new Mock<ITenantSequenceService>();
        sequenceB.SetupSequence(x => x.GetNextPaymentNumberAsync())
            .ReturnsAsync("PAY-000002")
            .ReturnsAsync("PAY-000003");

        var serviceA = new PaymentService(contextA, _tenantContext.Object, sequenceA.Object);
        var serviceB = new PaymentService(contextB, _tenantContext.Object, sequenceB.Object);
        var requestA = new CreatePaymentDto
        {
            InvoiceId = invoiceId,
            Amount = 60,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Method = PaymentMethod.Check
        };
        var requestB = new CreatePaymentDto
        {
            InvoiceId = invoiceId,
            Amount = 60,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Method = PaymentMethod.CreditCard
        };

        await serviceA.CreatePaymentAsync(requestA);
        var exception = await Assert.ThrowsAsync<ValidationException>(() => serviceB.CreatePaymentAsync(requestB));
        Assert.Contains("cannot exceed the balance due", exception.Message);

        Assert.DoesNotContain(contextB.ChangeTracker.Entries(),
            e => e.State is EntityState.Added or EntityState.Modified);
        await contextB.SaveChangesAsync();

        using var verifyContext = new ApplicationDbContext(options, _tenantContext.Object);
        var invoice = await verifyContext.Invoices.Include(i => i.Payments).SingleAsync(i => i.Id == invoiceId);
        var payments = await verifyContext.Payments.Where(p => p.InvoiceId == invoiceId).ToListAsync();
        var activities = await verifyContext.Activities.ToListAsync();
        var auditLogs = await verifyContext.AuditLogs.ToListAsync();

        Assert.Single(payments);
        Assert.Equal(60, payments.Sum(p => p.Amount));
        Assert.Equal(payments.Sum(p => p.Amount), invoice.AmountPaid);
        Assert.Equal(40, invoice.BalanceDue);
        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Null(invoice.PaidAt);
        Assert.Equal(2, activities.Count);
        Assert.Single(activities, a => a.EntityType == "Payment" && a.ActivityType == ActivityType.Created);
        Assert.Single(activities, a => a.EntityType == "Invoice" && a.ActivityType == ActivityType.PaymentReceived);
        Assert.Empty(auditLogs);
    }
}
