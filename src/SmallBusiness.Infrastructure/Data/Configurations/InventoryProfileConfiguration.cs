using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class InventoryProfileConfiguration : IEntityTypeConfiguration<InventoryProfile>
{
    public void Configure(EntityTypeBuilder<InventoryProfile> builder)
    {
        builder.HasKey(e => e.Id);
        
        builder.HasOne(e => e.CatalogItem)
            .WithOne()
            .HasForeignKey<InventoryProfile>(e => e.CatalogItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // One profile per CatalogItem per Business
        builder.HasIndex(e => new { e.BusinessId, e.CatalogItemId }).IsUnique();

        builder.Property(e => e.ReorderLevel).HasPrecision(18, 4);
        builder.Property(e => e.PreferredStockLevel).HasPrecision(18, 4);
    }
}
