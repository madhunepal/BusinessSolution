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
            var entries = context.ChangeTracker.Entries<InventoryStockLevel>()
                .Where(e => e.State == EntityState.Modified)
                .ToList();

            foreach (var entry in entries)
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
                
                // Simulate updating the RowVersion in the database
                entry.CurrentValues["RowVersion"] = Guid.NewGuid().ToByteArray();
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
