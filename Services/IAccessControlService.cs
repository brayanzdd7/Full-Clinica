using ClinicaAPI.Models;

namespace ClinicaAPI.Services;

public interface IAccessControlService
{
    int? GetUsuarioId();

    Task<Usuario?> GetUsuarioActualAsync(CancellationToken cancellationToken = default);

    Task<int?> GetPacienteIdActualAsync(CancellationToken cancellationToken = default);

    Task<int?> GetDoctorIdActualAsync(CancellationToken cancellationToken = default);

    Task<bool> EsAdminORecepcionistaAsync(CancellationToken cancellationToken = default);

    Task<bool> PuedeVerPacienteAsync(int pacienteId, CancellationToken cancellationToken = default);

    Task<bool> PuedeGestionarDoctorAsync(int doctorId, CancellationToken cancellationToken = default);

    Task<bool> PuedeAccederCitaAsync(int citaId, CancellationToken cancellationToken = default);

    Task<bool> PuedeAccederFacturaAsync(int facturaId, CancellationToken cancellationToken = default);

    Task<bool> PuedeAccederPagoPorCitaAsync(int citaId, CancellationToken cancellationToken = default);
}
