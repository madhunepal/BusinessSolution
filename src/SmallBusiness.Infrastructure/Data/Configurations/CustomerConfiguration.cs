using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(e => e.Id);
        
        builder.HasIndex(e => new { e.BusinessId, e.CustomerNumber }).IsUnique();
        
        builder.Property(e => e.CustomerNumber).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.PrimaryContactName).HasMaxLength(200);
        builder.Property(e => e.Email).HasMaxLength(255);
        builder.Property(e => e.PhoneNumber).HasMaxLength(50);
        
        builder.Property(e => e.AddressStreet).HasMaxLength(200);
        builder.Property(e => e.AddressCity).HasMaxLength(100);
        builder.Property(e => e.AddressState).HasMaxLength(100);
        builder.Property(e => e.AddressPostalCode).HasMaxLength(50);
        builder.Property(e => e.AddressCountry).HasMaxLength(100);
        
        builder.Property(e => e.Notes).HasMaxLength(2000);
    }
}
