using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Microsoft.Extensions.DependencyInjection;

namespace SmallBusiness.Application.Services;

public interface ITenantSequenceService
{
    Task<string> GetNextCustomerNumberAsync();
    Task<string> GetNextItemCodeAsync();
    Task<string> GetNextQuoteNumberAsync();
    Task<string> GetNextSalesOrderNumberAsync();
    Task<string> GetNextJobNumberAsync();
    Task<string> GetNextInvoiceNumberAsync();
}

public class TenantSequenceService : ITenantSequenceService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantContext _tenantContext;

    public TenantSequenceService(IServiceScopeFactory scopeFactory, ITenantContext tenantContext)
    {
        _scopeFactory = scopeFactory;
        _tenantContext = tenantContext;
    }

    public Task<string> GetNextCustomerNumberAsync()
    {
        return GenerateNextSequenceAsync("Customer", "CUST-");
    }

    public Task<string> GetNextItemCodeAsync()
    {
        return GenerateNextSequenceAsync("CatalogItem", "ITEM-");
    }

    public Task<string> GetNextQuoteNumberAsync()
    {
        return GenerateNextSequenceAsync("Quote", "QUOTE-");
    }

    public Task<string> GetNextSalesOrderNumberAsync()
    {
        return GenerateNextSequenceAsync("SalesOrder", "SO-");
    }

    public Task<string> GetNextJobNumberAsync()
    {
        return GenerateNextSequenceAsync("Job", "JOB-");
    }

    public Task<string> GetNextInvoiceNumberAsync()
    {
        return GenerateNextSequenceAsync("Invoice", "INV-");
    }

    private async Task<string> GenerateNextSequenceAsync(string entityType, string prefix)
    {
        var businessId = _tenantContext.CurrentBusinessId 
            ?? throw new UnauthorizedAccessException("No active business context.");
            
        int nextValue = 1;

        int maxRetries = 5;
        for (int i = 0; i < maxRetries; i++)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            var seq = await context.TenantSequences
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.BusinessId == businessId && s.EntityType == entityType);

            if (seq == null)
            {
                seq = new TenantSequence
                {
                    BusinessId = businessId,
                    EntityType = entityType,
                    CurrentValue = 1,
                    RowVersion = new byte[8]
                };
                context.TenantSequences.Add(seq);
            }
            else
            {
                seq.CurrentValue += 1;
                nextValue = seq.CurrentValue;
            }

            try
            {
                await context.SaveChangesAsync(default);
                return $"{prefix}{nextValue:D6}";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (i == maxRetries - 1) throw;
                await Task.Delay(Random.Shared.Next(10, 50));
            }
        }
        
        throw new InvalidOperationException($"Could not generate sequence for {entityType} due to high concurrency.");
    }
}
