using SmallBusiness.Application.DTOs.Appointments;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.Interfaces;

public interface IAppointmentService
{
    Task<Guid> CreateAppointmentAsync(CreateAppointmentRequest request, bool ignoreConflicts = false);
    Task<AppointmentDto> GetAppointmentAsync(Guid id);
    Task<List<AppointmentDto>> GetAppointmentsAsync(AppointmentSearchRequest request);
    Task UpdateAppointmentTimeAsync(Guid id, UpdateAppointmentTimeRequest request, bool ignoreConflicts = false);
    Task ChangeAppointmentStatusAsync(Guid id, AppointmentStatus status);
    Task AssignTechnicianAsync(Guid appointmentId, string userId, bool ignoreConflicts = false);
    Task RemoveTechnicianAsync(Guid appointmentId, string userId);
}
