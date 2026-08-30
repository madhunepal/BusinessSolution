using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasOne(p => p.Invoice)
            .WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.PaymentNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Amount).HasPrecision(18, 2);

        builder.Property(p => p.ReferenceNumber).HasMaxLength(100);

        builder.HasIndex(p => new { p.BusinessId, p.InvoiceId });
    }
}
