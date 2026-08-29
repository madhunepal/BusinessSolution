using SmallBusiness.Domain.Common;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Domain.Entities;

public class Appointment : BaseEntity, IHasBusinessId
{
    public Guid BusinessId { get; set; }
    
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    
    // DateTimeOffset to store unambiguous instants
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public string? Notes { get; set; }
    
    public DateTimeOffset? ActualStart { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    
    public ICollection<AppointmentAssignment> Assignments { get; set; } = new List<AppointmentAssignment>();
}
