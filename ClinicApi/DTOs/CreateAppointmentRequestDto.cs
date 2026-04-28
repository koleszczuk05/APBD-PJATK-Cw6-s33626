using System.ComponentModel.DataAnnotations;
using System.Data;

namespace ClinicApi.DTOs;

public class CreateAppointmentRequestDto
{
    public int IdPatient { get; set; }
    
    public int IdDoctor { get; set; }
    
    public DateTime AppointmentDate { get; set; }
    
    [Required, MaxLength(250)]
    public string Reason { get; set; } =  string.Empty;
}