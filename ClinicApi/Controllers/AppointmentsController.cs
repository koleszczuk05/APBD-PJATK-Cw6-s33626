using ClinicApi.DTOs;
using ClinicApi.Exceptions;
using ClinicApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClinicApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController(IAppointmentsService services) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAppointments([FromQuery] string? status, [FromQuery] string? patientLastName, CancellationToken cancellationToken)
    {
        return Ok(await services.GetAppointmentsAsync(status, patientLastName, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAppointmentByIdAsync([FromRoute] int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await services.GetAppointmentsByIdAsync(id, cancellationToken);

            return Ok(result);
        }
        catch (NotFoundException e)
        {
            var error = new ErrorResponseDto() { Message = e.Message };
            return NotFound(error);
        }
        catch (Exception e)
        {
            var error = new ErrorResponseDto{ Message = e.Message };
            return StatusCode(500, error);
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateAppointmentRequestDto createAppointmentRequestDto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var newAppointment = await services.CreateAppointmentAsync(createAppointmentRequestDto, cancellationToken);
            return Created($"/api/appointments/{newAppointment.IdAppointment}", newAppointment);
        }
        catch (NotFoundException e)
        {
            var error = new ErrorResponseDto() { Message = e.Message };
            return NotFound(error);
        }
        catch (ConflictException e)
        {
            var error = new ErrorResponseDto() { Message = e.Message };
            return Conflict(error);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] int id,
        [FromBody] UpdateAppointmentRequestDto updateAppointmentRequestDto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await services.UpdateAppointmentAsync(id, updateAppointmentRequestDto, cancellationToken);
            return NoContent();
        }
        catch (NotFoundException e)
        {
            var error = new ErrorResponseDto() { Message = e.Message };
            return NotFound(error);
        }
        catch (ConflictException e)
        {
            var error = new ErrorResponseDto() {  Message = e.Message };
            return Conflict(error);
        }
        catch (Exception e)
        {
            var error = new ErrorResponseDto { Message = e.Message };
            return StatusCode(500, error);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await services.RemoveAsync(id, cancellationToken);
            return NoContent();
        }
        catch (NotFoundException e)
        {
            var error = new ErrorResponseDto() { Message = e.Message };
            return NotFound(error);
        }
        catch (ConflictException e)
        {
            var error = new ErrorResponseDto() { Message = e.Message };
            return Conflict(error);
        }
        catch (Exception e)
        {
            var error = new ErrorResponseDto { Message = e.Message };
            return StatusCode(500, error);
        }
        
    }
}