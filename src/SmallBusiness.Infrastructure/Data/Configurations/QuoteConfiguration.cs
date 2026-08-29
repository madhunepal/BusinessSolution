using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuoteNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => new { x.BusinessId, x.QuoteNumber })
            .IsUnique();

        builder.Property(x => x.CustomerNameSnapshot)
            .HasMaxLength(200);

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
            
        builder.HasMany(x => x.Lines)
            .WithOne(x => x.Quote)
            .HasForeignKey(x => x.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
