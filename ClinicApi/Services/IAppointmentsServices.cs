using ClinicApi.DTOs;

namespace ClinicApi.Services;

public interface IAppointmentsService
{
    public Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName, CancellationToken cancellationToken = default);
    
    public Task<AppointmentDetailsDto> GetAppointmentsByIdAsync(int id, CancellationToken cancellationToken = default);
    
    public Task<AppointmentDetailsDto> CreateAppointmentAsync(CreateAppointmentRequestDto createAppointmentRequestDto, CancellationToken cancellationToken = default);
}