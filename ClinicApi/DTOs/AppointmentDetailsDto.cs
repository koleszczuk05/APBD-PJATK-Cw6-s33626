namespace ClinicApi.DTOs;

public class AppointmentDetailsDto
{
    public int IdAppointment { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? InternalNotes { get; set; } =  string.Empty;
    public DateTime CreatedAt { get; set; }
    public string PatientFirstName { get; set; } = string.Empty;
    public string PatientLastName { get; set; } = string.Empty;
    public string DoctorFirstName { get; set; } = string.Empty;
    public string DoctorLastName { get; set; } = string.Empty;
    
       
}