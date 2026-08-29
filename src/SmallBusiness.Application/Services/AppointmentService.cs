using Microsoft.EntityFrameworkCore;
using SmallBusiness.Application.DTOs.Appointments;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmallBusiness.Application.Services;

public class AppointmentService : IAppointmentService
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    public AppointmentService(IApplicationDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> CreateAppointmentAsync(CreateAppointmentRequest request, bool ignoreConflicts = false)
    {
        if (request.End <= request.Start)
            throw new ValidationException("End time must be after Start time.");

        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException("Business context required.");

        // Validate Job
        var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == request.JobId && j.BusinessId == businessId);
        if (job == null)
            throw new ValidationException("Job not found.");
            
        if (job.Status == JobStatus.Completed || job.Status == JobStatus.Cancelled)
            throw new ValidationException($"Cannot schedule an appointment for a Job that is {job.Status}.");

        await CheckTechnicianOverlapsAsync(request.AssignedUserIds, request.Start, request.End, ignoreConflicts: ignoreConflicts);

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            JobId = request.JobId,
            Start = request.Start,
            End = request.End,
            Notes = request.Notes,
            Status = AppointmentStatus.Scheduled
        };

        foreach (var userId in request.AssignedUserIds)
        {
            // Validate BusinessUser
            var isAuthorizedUser = await _context.BusinessUsers
                .AnyAsync(u => u.BusinessId == businessId && u.UserId == userId && u.IsActive);
                
            if (!isAuthorizedUser)
                throw new ValidationException($"User {userId} is not authorized for this business.");

            appointment.Assignments.Add(new AppointmentAssignment
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                AppointmentId = appointment.Id,
                UserId = userId,
                AssignedAt = DateTimeOffset.UtcNow
            });
        }

        _context.Appointments.Add(appointment);
        
        // Log Activity
        _context.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = job.Id,
            EntityType = "Job",
            Description = $"Appointment scheduled for {request.Start:g}.",
            CreatedBy = _tenantContext.UserId
            
        });

        await _context.SaveChangesAsync();
        return appointment.Id;
    }

    public async Task<AppointmentDto> GetAppointmentAsync(Guid id)
    {
        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException();
        var appointment = await _context.Appointments
            .Include(a => a.Job)
            .Include(a => a.Assignments)
            .FirstOrDefaultAsync(a => a.Id == id && a.BusinessId == businessId)
            ?? throw new KeyNotFoundException("Appointment not found.");

        return MapToDto(appointment);
    }

    public async Task<List<AppointmentDto>> GetAppointmentsAsync(AppointmentSearchRequest request)
    {
        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException();
        var query = _context.Appointments
            .Include(a => a.Job)
            .Include(a => a.Assignments)
            .Where(a => a.BusinessId == businessId)
            .AsQueryable();

        if (request.StartDate.HasValue)
        {
            query = query.Where(a => a.Start >= request.StartDate.Value);
        }
        
        if (request.EndDate.HasValue)
        {
            query = query.Where(a => a.Start <= request.EndDate.Value);
        }
        
        if (request.Status.HasValue)
        {
            query = query.Where(a => a.Status == request.Status.Value);
        }

        if (!string.IsNullOrEmpty(request.TechnicianUserId))
        {
            query = query.Where(a => a.Assignments.Any(assign => assign.UserId == request.TechnicianUserId));
        }

        var results = await query.OrderBy(a => a.Start).ToListAsync();
        
        return results.Select(MapToDto).ToList();
    }

    public async Task UpdateAppointmentTimeAsync(Guid id, UpdateAppointmentTimeRequest request, bool ignoreConflicts = false)
    {
        if (request.End <= request.Start)
            throw new ValidationException("End time must be after Start time.");

        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException();
        var appointment = await _context.Appointments
            .Include(a => a.Assignments)
            .Include(a => a.Job)
            .FirstOrDefaultAsync(a => a.Id == id && a.BusinessId == businessId)
            ?? throw new KeyNotFoundException("Appointment not found.");

        if (appointment.Status == AppointmentStatus.Completed || appointment.Status == AppointmentStatus.Cancelled)
            throw new ValidationException($"Cannot reschedule an appointment that is {appointment.Status}.");
            
        var assignedUsers = appointment.Assignments.Select(x => x.UserId).ToList();
        await CheckTechnicianOverlapsAsync(assignedUsers, request.Start, request.End, appointment.Id, ignoreConflicts: ignoreConflicts);

        appointment.Start = request.Start;
        appointment.End = request.End;

        _context.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = appointment.JobId,
            EntityType = "Job",
            Description = $"Appointment rescheduled to {request.Start:g}.",
            CreatedBy = _tenantContext.UserId
            
        });

        await _context.SaveChangesAsync();
    }

    public async Task ChangeAppointmentStatusAsync(Guid id, AppointmentStatus status)
    {
        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException();
        var appointment = await _context.Appointments
            .Include(a => a.Job)
            .FirstOrDefaultAsync(a => a.Id == id && a.BusinessId == businessId)
            ?? throw new KeyNotFoundException("Appointment not found.");

        if (appointment.Status == AppointmentStatus.Completed || appointment.Status == AppointmentStatus.Cancelled)
            throw new ValidationException($"Appointment is {appointment.Status} and cannot be changed.");

        if (status == AppointmentStatus.InProgress && appointment.Status == AppointmentStatus.Scheduled)
        {
            appointment.ActualStart = DateTimeOffset.UtcNow;
            
            // Interaction with Job lifecycle
            if (appointment.Job.Status == JobStatus.Ready || appointment.Job.Status == JobStatus.Draft) // Modified rule per request: "Do NOT automatically transition Draft -> InProgress. A Draft Job is not operationally ready and an Appointment must not bypass that lifecycle invariant." Wait, let's fix this below.
            {
                // To be fixed in code block
            }
        }
        else if (status == AppointmentStatus.Completed && appointment.Status == AppointmentStatus.InProgress)
        {
            appointment.CompletedAt = DateTimeOffset.UtcNow;
            _context.Activities.Add(new Activity
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                EntityId = appointment.JobId,
                EntityType = "Job",
                Description = "Appointment completed.",
                CreatedBy = _tenantContext.UserId
                
            });
        }
        else if (status == AppointmentStatus.Cancelled)
        {
            appointment.CancelledAt = DateTimeOffset.UtcNow;
            _context.Activities.Add(new Activity
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                EntityId = appointment.JobId,
                EntityType = "Job",
                Description = "Appointment cancelled.",
                CreatedBy = _tenantContext.UserId
                
            });
        }
        else
        {
            throw new ValidationException($"Invalid status transition from {appointment.Status} to {status}.");
        }

        appointment.Status = status;

        if (status == AppointmentStatus.InProgress)
        {
            if (appointment.Job.Status == JobStatus.Ready)
            {
                appointment.Job.Status = JobStatus.InProgress;
                appointment.Job.ActualStartDate = DateTime.UtcNow;
                _context.Activities.Add(new Activity
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    EntityId = appointment.JobId,
                    EntityType = "Job",
                    Description = "Job started automatically via Appointment.",
                    CreatedBy = _tenantContext.UserId
                    
                });
            }
            else if (appointment.Job.Status == JobStatus.Draft)
            {
                throw new ValidationException("Cannot start an appointment for a Draft Job. Job must be Ready first.");
            }
            
            _context.Activities.Add(new Activity
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                EntityId = appointment.JobId,
                EntityType = "Job",
                Description = "Appointment started.",
                CreatedBy = _tenantContext.UserId
                
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task AssignTechnicianAsync(Guid appointmentId, string userId, bool ignoreConflicts = false)
    {
        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException();
        var appointment = await _context.Appointments
            .Include(a => a.Assignments)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.BusinessId == businessId)
            ?? throw new KeyNotFoundException("Appointment not found.");

        if (appointment.Status == AppointmentStatus.Completed || appointment.Status == AppointmentStatus.Cancelled)
            throw new ValidationException($"Cannot assign technician to a {appointment.Status} appointment.");

        if (appointment.Assignments.Any(a => a.UserId == userId))
            throw new ValidationException("User is already assigned to this appointment.");

        var isAuthorizedUser = await _context.BusinessUsers
            .AnyAsync(u => u.BusinessId == businessId && u.UserId == userId && u.IsActive);
                
        if (!isAuthorizedUser)
            throw new ValidationException($"User {userId} is not authorized for this business.");

        await CheckTechnicianOverlapsAsync(new List<string> { userId }, appointment.Start, appointment.End, ignoreConflicts: ignoreConflicts);

        appointment.Assignments.Add(new AppointmentAssignment
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            AppointmentId = appointmentId,
            UserId = userId,
            AssignedAt = DateTimeOffset.UtcNow
        });

        _context.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = appointment.JobId,
            EntityType = "Job",
            Description = "Technician assigned to appointment.",
            CreatedBy = _tenantContext.UserId
            
        });

        await _context.SaveChangesAsync();
    }

    public async Task RemoveTechnicianAsync(Guid appointmentId, string userId)
    {
        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException();
        var appointment = await _context.Appointments
            .Include(a => a.Assignments)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.BusinessId == businessId)
            ?? throw new KeyNotFoundException("Appointment not found.");

        if (appointment.Status == AppointmentStatus.Completed || appointment.Status == AppointmentStatus.Cancelled)
            throw new ValidationException($"Cannot remove technician from a {appointment.Status} appointment.");

        var assignment = appointment.Assignments.FirstOrDefault(a => a.UserId == userId);
        if (assignment == null)
            throw new ValidationException("User is not assigned to this appointment.");

        appointment.Assignments.Remove(assignment);
        
        _context.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = appointment.JobId,
            EntityType = "Job",
            Description = "Technician removed from appointment.",
            CreatedBy = _tenantContext.UserId
            
        });

        await _context.SaveChangesAsync();
    }
    
    private async Task CheckTechnicianOverlapsAsync(List<string> userIds, DateTimeOffset start, DateTimeOffset end, Guid? excludeAppointmentId = null, bool ignoreConflicts = false)
    {
        if (!userIds.Any() || ignoreConflicts) return;
        
        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException();

        // Detect overlapping appointments
        // existing.Start < proposed.End AND existing.End > proposed.Start
        var overlappingQuery = _context.AppointmentAssignments
            .Include(a => a.Appointment)
            .Where(a => a.BusinessId == businessId && 
                        userIds.Contains(a.UserId) &&
                        a.Appointment.Status != AppointmentStatus.Cancelled &&
                        a.Appointment.Start < end && 
                        a.Appointment.End > start);

        if (excludeAppointmentId.HasValue)
        {
            overlappingQuery = overlappingQuery.Where(a => a.AppointmentId != excludeAppointmentId.Value);
        }
        
        var overlaps = await overlappingQuery.ToListAsync();

        if (overlaps.Any())
        {
            // For V1, the check is advisory, but the application layer returns a warning flag or throws a specific exception that the UI can catch.
            // Since the requirements state: "If conflicts exist: return/display a clear scheduling warning... allow an authorized scheduler to proceed anyway. The conflict check is advisory in V1."
            // To implement this in a simple service, we might throw a WarningException if a special flag isn't passed, but the current methods don't take an "override" flag.
            // I'll throw a specific exception. The UI will catch it, and if it's an overlap, the UI can pass `ignoreOverlap = true` if we add that flag.
            throw new ValidationException("SCHEDULING_CONFLICT: One or more technicians have overlapping appointments.");
        }
    }

    private AppointmentDto MapToDto(Appointment appointment)
    {
        return new AppointmentDto
        {
            Id = appointment.Id,
            JobId = appointment.JobId,
            JobNumber = appointment.Job?.JobNumber ?? string.Empty,
            JobTitle = appointment.Job?.Title ?? string.Empty,
            CustomerName = appointment.Job?.CustomerNameSnapshot ?? string.Empty,
            ServiceStreet = appointment.Job?.ServiceStreet ?? string.Empty,
            ServiceCity = appointment.Job?.ServiceCity ?? string.Empty,
            ServiceState = appointment.Job?.ServiceState ?? string.Empty,
            ServicePostalCode = appointment.Job?.ServicePostalCode ?? string.Empty,
            Start = appointment.Start,
            End = appointment.End,
            Status = appointment.Status,
            Notes = appointment.Notes,
            Assignments = appointment.Assignments.Select(a => new AppointmentAssignmentDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserName = "Technician" // This could be enriched later
            }).ToList()
        };
    }
}
