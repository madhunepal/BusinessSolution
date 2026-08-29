using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(i => i.Id);
        
        builder.HasOne(i => i.SalesOrder)
            .WithMany()
            .HasForeignKey(i => i.SalesOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Customer)
            .WithMany()
            .HasForeignKey(i => i.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(i => i.InvoiceNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.Terms)
            .HasMaxLength(255);

        // Enforce one active Invoice per SalesOrder per Business
        builder.HasIndex(i => new { i.BusinessId, i.SalesOrderId })
            .IsUnique()
            .HasFilter($"[Status] != {(int)InvoiceStatus.Void}");

        builder.Property(i => i.TaxRate).HasPrecision(18, 4);
        builder.Property(i => i.Subtotal).HasPrecision(18, 2);
        builder.Property(i => i.DiscountAmount).HasPrecision(18, 2);
        builder.Property(i => i.TaxAmount).HasPrecision(18, 2);
        builder.Property(i => i.Total).HasPrecision(18, 2);
        builder.Property(i => i.AmountPaid).HasPrecision(18, 2);
        builder.Property(i => i.BalanceDue).HasPrecision(18, 2);
    }
}
