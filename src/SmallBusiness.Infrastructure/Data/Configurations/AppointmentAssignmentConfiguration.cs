using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class AppointmentAssignmentConfiguration : IEntityTypeConfiguration<AppointmentAssignment>
{
    public void Configure(EntityTypeBuilder<AppointmentAssignment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(450); // Matches IdentityUser Id length

        // Relationships
        builder.HasOne(a => a.Appointment)
            .WithMany(a => a.Assignments)
            .HasForeignKey(a => a.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique index per appointment and user
        builder.HasIndex(a => new { a.AppointmentId, a.UserId })
            .IsUnique();
    }
}
