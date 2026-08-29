using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.ItemCode)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);
            
        builder.Property(e => e.Description)
            .HasMaxLength(2000);
            
        builder.Property(e => e.Unit)
            .IsRequired()
            .HasMaxLength(50);
            
        // decimal(18, 4) for pricing
        builder.Property(e => e.Cost)
            .HasPrecision(18, 4);
            
        builder.Property(e => e.SellingPrice)
            .HasPrecision(18, 4);
            
        // Unique index on BusinessId + ItemCode
        builder.HasIndex(e => new { e.BusinessId, e.ItemCode })
            .IsUnique();

        builder.HasOne(e => e.Business)
            .WithMany()
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
