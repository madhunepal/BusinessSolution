using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Application.Tests;

public class InMemoryConcurrencyInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, 
        InterceptionResult<int> result, 
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context != null)
        {
            foreach (var entry in context.ChangeTracker.Entries<InventoryStockLevel>().Where(e => e.State == EntityState.Modified))
            {
                CheckRowVersion(entry);
            }

            foreach (var entry in context.ChangeTracker.Entries<Invoice>().Where(e => e.State == EntityState.Modified))
            {
                CheckRowVersion(entry);
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void CheckRowVersion(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var originalRowVersion = entry.OriginalValues.GetValue<byte[]>("RowVersion");
        var databaseEntry = entry.GetDatabaseValues();

        if (databaseEntry != null)
        {
            var databaseRowVersion = databaseEntry.GetValue<byte[]>("RowVersion");

            if (originalRowVersion == null || databaseRowVersion == null || !originalRowVersion.SequenceEqual(databaseRowVersion))
            {
                throw new DbUpdateConcurrencyException("Concurrency conflict detected by interceptor.");
            }
        }

        // Simulate SQL Server rowversion changing when a concurrency-protected row is updated.
        entry.CurrentValues["RowVersion"] = Guid.NewGuid().ToByteArray();
    }
}
