using System.ComponentModel.DataAnnotations;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.DTOs.Appointments;

public class AppointmentDto
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    
    // Projecting Job information for schedule views
    public string JobNumber { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    
    // Service Location projected from Job
    public string ServiceStreet { get; set; } = string.Empty;
    public string ServiceCity { get; set; } = string.Empty;
    public string ServiceState { get; set; } = string.Empty;
    public string ServicePostalCode { get; set; } = string.Empty;
    
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    
    public AppointmentStatus Status { get; set; }
    public string? Notes { get; set; }
    
    public List<AppointmentAssignmentDto> Assignments { get; set; } = new();
}

public class AppointmentAssignmentDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    // We can join this with Identity on the UI if needed, or project the name if we had it in BusinessUser
    public string UserName { get; set; } = string.Empty;
}

public class CreateAppointmentRequest
{
    [Required]
    public Guid JobId { get; set; }
    
    [Required]
    public DateTimeOffset Start { get; set; }
    
    [Required]
    public DateTimeOffset End { get; set; }
    
    public string? Notes { get; set; }
    
    public List<string> AssignedUserIds { get; set; } = new();
}

public class UpdateAppointmentTimeRequest
{
    [Required]
    public DateTimeOffset Start { get; set; }
    
    [Required]
    public DateTimeOffset End { get; set; }
}

public class AppointmentSearchRequest
{
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public string? TechnicianUserId { get; set; }
    public AppointmentStatus? Status { get; set; }
}
