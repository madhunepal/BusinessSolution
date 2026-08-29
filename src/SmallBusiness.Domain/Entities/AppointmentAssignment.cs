using SmallBusiness.Domain.Common;

namespace SmallBusiness.Domain.Entities;

public class AppointmentAssignment : BaseEntity, IHasBusinessId
{
    public Guid BusinessId { get; set; }
    
    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;
    
    // Identity User Id (must exist in BusinessUsers for the current BusinessId)
    public string UserId { get; set; } = string.Empty;
    
    public DateTimeOffset AssignedAt { get; set; }
}
