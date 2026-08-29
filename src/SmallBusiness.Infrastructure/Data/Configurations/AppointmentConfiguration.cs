using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(a => a.Id);

        // Max lengths
        builder.Property(a => a.Notes)
            .HasMaxLength(2000);

        // Relationships
        builder.HasOne(a => a.Job)
            .WithMany()
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.Restrict);

        // Enums
        builder.Property(a => a.Status)
            .HasConversion<int>();
            
        // Indexes
        builder.HasIndex(a => new { a.BusinessId, a.Start });
    }
}
