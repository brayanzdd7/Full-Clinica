using System.Security.Claims;
using ClinicaAPI.Constants;
using ClinicaAPI.Data;
using ClinicaAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicaAPI.Services;

public class AccessControlService : IAccessControlService
{
    private static bool EsAdminORecepcion(string? r) =>
        r == RoleNames.Admin || r == "Administrador" || r == RoleNames.Recepcionista;

    private readonly ClinicaContext _db;
    private readonly IHttpContextAccessor _http;

    public AccessControlService(ClinicaContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public int? GetUsuarioId()
    {
        var sub = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(sub, out var id) ? id : null;
    }

    public async Task<Usuario?> GetUsuarioActualAsync(CancellationToken cancellationToken = default)
    {
        var uid = GetUsuarioId();
        if (uid == null) return null;
        return await _db.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.UsuarioId == uid, cancellationToken);
    }

    public async Task<int?> GetPacienteIdActualAsync(CancellationToken cancellationToken = default)
    {
        var uid = GetUsuarioId();
        if (uid == null) return null;
        return await _db.Pacientes
            .AsNoTracking()
            .Where(p => p.UsuarioId == uid)
            .Select(p => (int?)p.PacienteId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int?> GetDoctorIdActualAsync(CancellationToken cancellationToken = default)
    {
        var uid = GetUsuarioId();
        if (uid == null) return null;
        return await _db.Doctores
            .AsNoTracking()
            .Where(d => d.UsuarioId == uid)
            .Select(d => (int?)d.DoctorId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> EsAdminORecepcionistaAsync(CancellationToken cancellationToken = default)
    {
        var u = await GetUsuarioActualAsync(cancellationToken);
        return EsAdminORecepcion(u?.Rol?.Nombre);
    }

    public async Task<bool> PuedeVerPacienteAsync(int pacienteId, CancellationToken cancellationToken = default)
    {
        var u = await GetUsuarioActualAsync(cancellationToken);
        var r = u?.Rol?.Nombre;
        if (r == null || u == null) return false;
        if (EsAdminORecepcion(r)) return true;

        if (r == RoleNames.Paciente)
        {
            var miPacienteId = await GetPacienteIdActualAsync(cancellationToken);
            return miPacienteId == pacienteId;
        }

        if (r == RoleNames.Doctor)
        {
            var doctorId = await GetDoctorIdActualAsync(cancellationToken);
            if (doctorId == null) return false;

            var tieneCita = await _db.Citas.AsNoTracking().AnyAsync(
                c => c.PacienteId == pacienteId && c.DoctorId == doctorId, cancellationToken);
            if (tieneCita) return true;

            return await _db.ConsultasMedicas.AsNoTracking().AnyAsync(
                c => c.PacienteId == pacienteId && c.DoctorId == doctorId, cancellationToken);
        }

        return false;
    }

    public async Task<bool> PuedeGestionarDoctorAsync(int doctorId, CancellationToken cancellationToken = default)
    {
        var u = await GetUsuarioActualAsync(cancellationToken);
        var r = u?.Rol?.Nombre;
        if (r == null) return false;
        if (EsAdminORecepcion(r)) return true;
        if (r == RoleNames.Doctor)
        {
            var mio = await GetDoctorIdActualAsync(cancellationToken);
            return mio == doctorId;
        }
        return false;
    }

    public async Task<bool> PuedeAccederCitaAsync(int citaId, CancellationToken cancellationToken = default)
    {
        var cita = await _db.Citas.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CitaId == citaId, cancellationToken);
        if (cita == null) return false;

        var u = await GetUsuarioActualAsync(cancellationToken);
        var r = u?.Rol?.Nombre;
        if (r == null || u == null) return false;
        if (EsAdminORecepcion(r)) return true;

        if (r == RoleNames.Paciente)
        {
            var miPacienteId = await GetPacienteIdActualAsync(cancellationToken);
            return miPacienteId == cita.PacienteId;
        }

        if (r == RoleNames.Doctor)
        {
            var miDoctorId = await GetDoctorIdActualAsync(cancellationToken);
            return miDoctorId == cita.DoctorId;
        }

        return false;
    }

    public async Task<bool> PuedeAccederFacturaAsync(int facturaId, CancellationToken cancellationToken = default)
    {
        var f = await _db.Facturas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.FacturaId == facturaId, cancellationToken);
        if (f == null) return false;

        var u = await GetUsuarioActualAsync(cancellationToken);
        var r = u?.Rol?.Nombre;
        if (r == null) return false;
        if (EsAdminORecepcion(r)) return true;

        if (r == RoleNames.Paciente)
            return await PuedeVerPacienteAsync(f.PacienteId, cancellationToken);

        if (r == RoleNames.Doctor)
        {
            // ── FIX CS1503: CitaId ahora es int? (nullable) ──────────────
            // Si la factura tiene cita, verificar acceso a esa cita.
            // Si no tiene cita (venta directa), el doctor no tiene acceso a facturas de ventas.
            if (f.CitaId.HasValue)
                return await PuedeAccederCitaAsync(f.CitaId.Value, cancellationToken);

            return false;
        }

        return false;
    }

    public async Task<bool> PuedeAccederPagoPorCitaAsync(int citaId, CancellationToken cancellationToken = default)
    {
        return await PuedeAccederCitaAsync(citaId, cancellationToken);
    }
}