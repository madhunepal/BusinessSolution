using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmallBusiness.Application.DTOs.Payments;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantSequenceService _sequenceService;
    private readonly IPermissionService? _permissionService;

    public PaymentService(
        IApplicationDbContext context,
        ITenantContext tenantContext,
        ITenantSequenceService sequenceService,
        IPermissionService? permissionService = null)
    {
        _context = context;
        _tenantContext = tenantContext;
        _sequenceService = sequenceService;
        _permissionService = permissionService;
    }

    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentDto request)
    {
        await EnsurePermissionAsync("Payments.Record");
        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException("Business context required.");

        if (request.Amount <= 0)
            throw new ValidationException("Payment amount must be greater than zero.");

        // We use a retry loop for concurrency on the Invoice settlement.
        int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(inv => inv.Payments)
                    .FirstOrDefaultAsync(inv => inv.Id == request.InvoiceId && inv.BusinessId == businessId)
                    ?? throw new KeyNotFoundException("Invoice not found.");

                if (invoice.Status == InvoiceStatus.Void)
                    throw new ValidationException("Cannot process payment for a voided invoice.");
                
                if (invoice.Status == InvoiceStatus.Draft)
                    throw new ValidationException("Cannot process payment for a draft invoice. Please send the invoice first.");

                if (invoice.Status == InvoiceStatus.Paid)
                    throw new ValidationException("Invoice is already fully paid.");

                if (request.Amount > invoice.BalanceDue)
                    throw new ValidationException($"Payment amount ({request.Amount:C}) cannot exceed the balance due ({invoice.BalanceDue:C}).");

                var paymentNumber = await _sequenceService.GetNextPaymentNumberAsync();

                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    PaymentNumber = paymentNumber,
                    InvoiceId = invoice.Id,
                    Amount = request.Amount,
                    PaymentDate = request.PaymentDate,
                    Method = request.Method,
                    ReferenceNumber = request.ReferenceNumber,
                    Notes = request.Notes
                };

                _context.Payments.Add(payment);

                // Update invoice settlement
                invoice.AmountPaid += request.Amount;
                invoice.BalanceDue = invoice.Total - invoice.AmountPaid;

                if (invoice.BalanceDue == 0)
                {
                    invoice.Status = InvoiceStatus.Paid;
                    invoice.PaidAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    invoice.Status = InvoiceStatus.PartiallyPaid;
                }

                var actionDesc = invoice.Status == InvoiceStatus.Paid ? "Paid" : "PartiallyPaid";

                _context.Activities.Add(new Activity
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    EntityId = payment.Id,
                    EntityType = "Payment",
                    ActivityType = ActivityType.Created,
                    Description = $"Payment {payment.PaymentNumber} of {payment.Amount:C} received via {payment.Method}.",
                    CreatedBy = _tenantContext.UserId
                });

                _context.Activities.Add(new Activity
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    EntityId = invoice.Id,
                    EntityType = "Invoice",
                    ActivityType = ActivityType.PaymentReceived,
                    Description = $"Invoice marked as {actionDesc} after receiving payment {payment.PaymentNumber}.",
                    CreatedBy = _tenantContext.UserId
                });

                await _context.SaveChangesAsync();

                return MapToDto(payment);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (i == maxRetries - 1)
                    throw new InvalidOperationException("Failed to process payment due to concurrent updates on the invoice.");
                
                // Clear the tracker to reload the invoice fresh in the next iteration
                _context.Invoices.Local.Clear();
                _context.Payments.Local.Clear();
                await Task.Delay(Random.Shared.Next(50, 150));
            }
        }

        throw new InvalidOperationException("Failed to process payment.");
    }

    public async Task<List<PaymentDto>> GetPaymentsForInvoiceAsync(Guid invoiceId)
    {
        await EnsurePermissionAsync("Payments.View");
        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException();

        var payments = await _context.Payments
            .Where(p => p.InvoiceId == invoiceId && p.BusinessId == businessId)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync();

        return payments.Select(MapToDto).ToList();
    }

    public async Task<PaymentDto> GetPaymentAsync(Guid id)
    {
        await EnsurePermissionAsync("Payments.View");
        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException();

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == businessId)
            ?? throw new KeyNotFoundException("Payment not found.");

        return MapToDto(payment);
    }

    private static PaymentDto MapToDto(Payment payment)
    {
        return new PaymentDto
        {
            Id = payment.Id,
            PaymentNumber = payment.PaymentNumber,
            InvoiceId = payment.InvoiceId,
            Amount = payment.Amount,
            PaymentDate = payment.PaymentDate,
            Method = payment.Method,
            ReferenceNumber = payment.ReferenceNumber,
            Notes = payment.Notes,
            CreatedAt = payment.CreatedAt
        };
    }

    private Task EnsurePermissionAsync(string permission) =>
        _permissionService?.EnsurePermissionAsync(permission) ?? Task.CompletedTask;
}
