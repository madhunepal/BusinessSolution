using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SalesOrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => new { x.BusinessId, x.SalesOrderNumber })
            .IsUnique();

        builder.Property(x => x.CustomerNumberSnapshot)
            .HasMaxLength(50);

        builder.Property(x => x.CustomerNameSnapshot)
            .HasMaxLength(200);

        builder.Property(x => x.CustomerEmailSnapshot)
            .HasMaxLength(200);

        builder.Property(x => x.CustomerPhoneSnapshot)
            .HasMaxLength(50);

        builder.Property(x => x.Notes)
            .HasMaxLength(4000);

        builder.Property(x => x.TaxRate)
            .HasPrecision(18, 4);

        builder.Property(x => x.Subtotal)
            .HasPrecision(18, 4);

        builder.Property(x => x.DiscountAmount)
            .HasPrecision(18, 4);

        builder.Property(x => x.TaxAmount)
            .HasPrecision(18, 4);

        builder.Property(x => x.Total)
            .HasPrecision(18, 4);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Quote)
            .WithMany()
            .HasForeignKey(x => x.QuoteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Enforce 1-to-1 Quote -> SalesOrder conversion safely at the DB level
        builder.HasIndex(x => x.QuoteId)
            .IsUnique()
            .HasFilter("[QuoteId] IS NOT NULL");
            
        builder.HasMany(x => x.Lines)
            .WithOne(x => x.SalesOrder)
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
