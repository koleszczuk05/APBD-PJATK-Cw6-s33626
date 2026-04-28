using System.Text;
using ClinicApi.DTOs;
using ClinicApi.Exceptions;
using Microsoft.Data.SqlClient;

namespace ClinicApi.Services;

public class AppointmentServices(IConfiguration configuration) : IAppointmentsService
{
    public async Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName, CancellationToken cancellationToken)
    {
        var result = new List<AppointmentListDto>();

        var sqlCommand = new StringBuilder("""
                                           select a.IdAppointment, a.AppointmentDate, a.Status, a.Reason, p.FirstName + N' ' + p.LastName As PatientFullName, p.Email
                                           from dbo.Appointments a
                                           join dbo.Patients p on a.IdPatient = p.IdPatient
                                           """);

        var conditions = new List<string>();

        var parameters = new List<SqlParameter>();

        if (status is not null)
        {
            conditions.Add("a.Status = @Status");
            parameters.Add(new SqlParameter("@Status", status));
        }

        if (patientLastName is not null)
        {
            conditions.Add("p.LastName = @PatientLastName");
            parameters.Add(new SqlParameter("@PatientLastName", patientLastName));
        }

        if (parameters.Count > 0)
        {
            sqlCommand.Append(" WHERE ");
            sqlCommand.Append(string.Join(" AND ", conditions));
        }

        sqlCommand.Append(" ORDER BY a.AppointmentDate");
        
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();

        command.Connection = connection;
        command.CommandText = sqlCommand.ToString();
        command.Parameters.AddRange(parameters.ToArray());
        
        await connection.OpenAsync(cancellationToken);
        
        var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new AppointmentListDto
            {
                IdAppointment = reader.GetInt32(0),
                AppointmentDate = reader.GetDateTime(1),
                Status = reader.GetString(2),
                Reason = reader.GetString(3),
                PatientFullName = reader.GetString(4),
                PatientEmail = reader.GetString(5),
            });
        }

        return result;
    }

    public async Task<AppointmentDetailsDto> GetAppointmentsByIdAsync(int id, CancellationToken cancellationToken)
    {
        AppointmentDetailsDto? result = null;
        
        const string sql = """
                       select a.IdAppointment, a.AppointmentDate, a.Status, a.Reason, a.InternalNotes, a.CreatedAt,
                       p.FirstName AS PatientFirstName, p.LastName AS PatientLastName,
                       d.FirstName AS DoctorFirstName, d.LastName AS DoctorLastName
                       from dbo.Appointments a
                       join dbo.Patients p on a.IdPatient = p.IdPatient
                       join dbo.Doctors d on a.IdDoctor = d.IdDoctor
                       WHERE a.IdAppointment = @Id;
                       """;

        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();

        command.Connection = connection;
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Id", id);

        await connection.OpenAsync(cancellationToken);

        var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result ??= new AppointmentDetailsDto
            {
                IdAppointment = reader.GetInt32(0),
                Date = reader.GetDateTime(1),
                Status = reader.GetString(2),
                Reason = reader.GetString(3),
                InternalNotes = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAt = reader.GetDateTime(5),
                PatientFirstName = reader.GetString(6),
                PatientLastName = reader.GetString(7),
                DoctorFirstName = reader.GetString(8),
                DoctorLastName = reader.GetString(9),
            };
        }

        if (result is null)
        {
            throw new NotFoundException($"There is no Appointment with this Id. {id}");
        }
        
        return result;
    }

    public async Task<AppointmentDetailsDto> CreateAppointmentAsync(CreateAppointmentRequestDto createAppointmentRequestDto,
        CancellationToken cancellationToken = default)
    {
        if (createAppointmentRequestDto.AppointmentDate < DateTime.Now)
        {
            throw new ConflictException("Appointment date cannot be in the past");
        }
        
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();
        
        await connection.OpenAsync(cancellationToken);
        
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        command.Connection =  connection;
        command.Transaction = (SqlTransaction)transaction;

        try
        {
            command.CommandText = "select FirstName, LastName, IsActive from Patients WHERE IdPatient = @Id;";
            command.Parameters.AddWithValue("@Id", createAppointmentRequestDto.IdPatient);
            string pFirst, pLast;
            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new NotFoundException("Patient does not exist");
                }

                if (!reader.GetBoolean(2))
                {
                    throw new ConflictException("Patient is not active");
                }

                pFirst = reader.GetString(0);
                pLast = reader.GetString(1);
            }

            command.Parameters.Clear();

            command.CommandText = "select FirstName, LastName, IsActive from Doctors WHERE IdDoctor = @Id;";
            command.Parameters.AddWithValue("@Id", createAppointmentRequestDto.IdDoctor);
            string dFirst, dLast;
            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new NotFoundException("Doctor does not exist");
                }

                if (!reader.GetBoolean(2))
                {
                    throw new ConflictException("Doctor is not active");
                }

                dFirst = reader.GetString(0);
                dLast = reader.GetString(1);
            }

            command.Parameters.Clear();

            command.CommandText =
                "select 1 from Appointments where AppointmentDate = @AppointmentDate and IdDoctor = @IdDoctor;";
            command.Parameters.AddWithValue("@IdDoctor", createAppointmentRequestDto.IdDoctor);
            command.Parameters.AddWithValue("@AppointmentDate", createAppointmentRequestDto.AppointmentDate);
            var AppointmentExistsD = await command.ExecuteScalarAsync(cancellationToken);

            if (AppointmentExistsD is not null)
            {
                throw new ConflictException("Appointment is already booked in that date");
            }

            command.Parameters.Clear();
            
            command.CommandText =
                "select 1 from Appointments where AppointmentDate = @AppointmentDate and IdPatient = @IdPatient;";
            command.Parameters.AddWithValue("@IdPatient", createAppointmentRequestDto.IdPatient);
            command.Parameters.AddWithValue("@AppointmentDate", createAppointmentRequestDto.AppointmentDate);
            var AppointmentExistsP = await command.ExecuteScalarAsync(cancellationToken);

            if (AppointmentExistsP is not null)
            {
                throw new ConflictException("Appointment is already booked in that date");
            }

            command.Parameters.Clear();

            command.CommandText = """
                                  insert into Appointments(IdPatient, IdDoctor, AppointmentDate, Status, Reason)
                                  output inserted.IdAppointment, inserted.CreatedAt
                                  values(@IdPatient, @IdDoctor, @AppointmentDate, @Status, @Reason)
                                  """;

            command.Parameters.AddWithValue("@IdPatient", createAppointmentRequestDto.IdPatient);
            command.Parameters.AddWithValue("@IdDoctor", createAppointmentRequestDto.IdDoctor);
            command.Parameters.AddWithValue("@AppointmentDate", createAppointmentRequestDto.AppointmentDate);
            command.Parameters.AddWithValue("@Status", "Scheduled");
            command.Parameters.AddWithValue("@Reason", createAppointmentRequestDto.Reason);

            int newId;
            DateTime createdAt;

            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                await reader.ReadAsync(cancellationToken);
                newId = reader.GetInt32(0);
                createdAt = reader.GetDateTime(1);
            }
            
            command.Parameters.Clear();

            await transaction.CommitAsync(cancellationToken);

            return new AppointmentDetailsDto
            {
                IdAppointment = newId,
                Date = createAppointmentRequestDto.AppointmentDate,
                Status = "Scheduled",
                Reason = createAppointmentRequestDto.Reason,
                CreatedAt = createdAt,
                PatientFirstName = pFirst,
                PatientLastName = pLast,
                DoctorFirstName = dFirst,
                DoctorLastName = dLast,
                InternalNotes = null
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        
    }

    public async Task UpdateAppointmentAsync(int id, UpdateAppointmentRequestDto updateAppointmentRequestDto,
        CancellationToken cancellationToken = default)
    {
        if (updateAppointmentRequestDto.Status != "Scheduled" && updateAppointmentRequestDto.Status != "Cancelled" &&
            updateAppointmentRequestDto.Status != "Completed")
        {
            throw new ConflictException("This Status is not permitted");
        }
        
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();
        
        command.Connection = connection;
        await command.Connection.OpenAsync(cancellationToken);

        command.CommandText = "select Status, AppointmentDate from Appointments where IdAppointment = @IdAppointment;";
        command.Parameters.AddWithValue("@IdAppointment", id);
        
        string currentStatus;
        DateTime currentDate;

        using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new NotFoundException("Appointment does not exist");
            }
            currentStatus = reader.GetString(0);
            currentDate = reader.GetDateTime(1);
        }
        
        command.Parameters.Clear();

        command.CommandText = "select 1 from Patients where IdPatient = @IdPatient and IsActive = 1;";
        command.Parameters.AddWithValue("@IdPatient", updateAppointmentRequestDto.IdPatient);
        
        var existsPatient = await command.ExecuteScalarAsync(cancellationToken);
        if (existsPatient is null)
        {
            throw new NotFoundException("Patient does not exist");
        }
        command.Parameters.Clear();
        
        command.CommandText = "select 1 from Doctors where IdDoctor = @IdDoctor and IsActive = 1;";
        command.Parameters.AddWithValue("@IdDoctor", updateAppointmentRequestDto.IdDoctor);
        
        var existsDoctor = await command.ExecuteScalarAsync(cancellationToken);
        if (existsDoctor is null)
        {
            throw new NotFoundException("Doctor does not exist");
        }

        command.Parameters.Clear();
        
        if (currentStatus == "Completed" && currentDate != updateAppointmentRequestDto.AppointmentDate)
        {
            throw new ConflictException("Appointment is already completed and you are trying to change the date");    
        }
        
        command.CommandText = "select 1 from Appointments where IdDoctor = @IdDoctor and AppointmentDate = @AppointmentDate and IdAppointment <> @IdAppointment;";
        command.Parameters.AddWithValue("@IdDoctor", updateAppointmentRequestDto.IdDoctor);
        command.Parameters.AddWithValue("@AppointmentDate", updateAppointmentRequestDto.AppointmentDate);
        command.Parameters.AddWithValue("@IdAppointment", id);
        
        var existsVisit = await command.ExecuteScalarAsync(cancellationToken);

        if (existsVisit is not null)
        {
            throw new ConflictException("Doctor already has a visit in this time");
        }
        
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        command.Transaction = (SqlTransaction)transaction;
        try
        {
            command.CommandText = """
                                  update Appointments set
                                  IdPatient = @IdPatient,
                                  IdDoctor = @IdDoctor,
                                  AppointmentDate = @AppointmentDate,
                                  Status = @Status,
                                  Reason = @Reason,
                                  InternalNotes = @InternalNotes
                                  where IdAppointment = @Id;
                                  """;

            command.Parameters.AddWithValue("@IdPatient", updateAppointmentRequestDto.IdPatient);
            command.Parameters.AddWithValue("@IdDoctor", updateAppointmentRequestDto.IdDoctor);
            command.Parameters.AddWithValue("@AppointmentDate", updateAppointmentRequestDto.AppointmentDate);
            command.Parameters.AddWithValue("@Status", updateAppointmentRequestDto.Status);
            command.Parameters.AddWithValue("@Reason", updateAppointmentRequestDto.Reason);
            command.Parameters.AddWithValue("@InternalNotes", (object)updateAppointmentRequestDto.InternalNotes ?? DBNull.Value);
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();
        command.Connection = connection;
        await command.Connection.OpenAsync(cancellationToken);
        
        command.CommandText = "select Status from Appointments where IdAppointment = @IdAppointment;";
        command.Parameters.AddWithValue("@IdAppointment", id);
        var CurrentStatus = await command.ExecuteScalarAsync(cancellationToken);
        if (CurrentStatus is null)
        {
            throw new NotFoundException("Appointment does not exist");
        }

        if (CurrentStatus.ToString() == "Completed")
        {
            throw new ConflictException("Cannot delete a completed appointment");
        }
        
        command.Parameters.Clear();
        
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        command.Transaction = (SqlTransaction)transaction;

        try
        {
            command.CommandText = "delete from Appointments where IdAppointment =  @IdAppointment;";
            command.Parameters.AddWithValue("IdAppointment", id);

            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}