using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClinicaAPI.Constants;
using ClinicaAPI.Data;
using ClinicaAPI.Models;
using ClinicaAPI.Services;

namespace ClinicaAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DoctoresController : ControllerBase
{
    private readonly ClinicaContext _db;
    private readonly IAccessControlService _access;

    public DoctoresController(ClinicaContext db, IAccessControlService access)
    {
        _db = db;
        _access = access;
    }

    // ── GET /api/doctores ──────────────────────────────────────
[HttpGet]
public async Task<IActionResult> GetAll([FromQuery] bool incluirInactivos = false)
{
    var query = _db.Doctores
        .Include(d => d.Usuario)
        .Include(d => d.Especialidad)
        .Include(d => d.EstadoMedico)
        .AsQueryable();

    if (!incluirInactivos)
        query = query.Where(d => d.Usuario!.Activo);

    return Ok(await query.ToListAsync());
}
    // ── GET /api/doctores/{id} ─────────────────────────────────
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!await _access.PuedeGestionarDoctorAsync(id))
            return Forbid();

        var doctor = await _db.Doctores
            .Include(d => d.Usuario)
            .Include(d => d.Especialidad)
            .Include(d => d.EstadoMedico)
            .FirstOrDefaultAsync(d => d.DoctorId == id);

        return doctor == null ? NotFound() : Ok(doctor);
    }

    // ── PUT /api/doctores/{id}/estado ──────────────────────────
    [HttpPut("{id}/estado")]
    public async Task<IActionResult> ActualizarEstado(int id, [FromBody] ActualizarEstadoDto dto)
    {
        if (!await _access.PuedeGestionarDoctorAsync(id))
            return Forbid();

        var estado = await _db.EstadoMedico.FirstOrDefaultAsync(e => e.DoctorId == id);
        if (estado == null) return NotFound("Estado médico no encontrado.");

        estado.Estado = dto.Estado;
        estado.Observacion = dto.Observacion;
        estado.FechaHoraActualizacion = DateTime.Now;

        await _db.SaveChangesAsync();
        return Ok(estado);
    }

    // ── GET /api/doctores/{id}/historial ───────────────────────
    // Historial completo del doctor: citas atendidas, recetas emitidas, pagos recibidos.
    [HttpGet("{id}/historial")]
    public async Task<IActionResult> GetHistorial(int id)
    {
        if (!await _access.PuedeGestionarDoctorAsync(id))
            return Forbid();

        // Citas atendidas
        var citas = await _db.Citas
            .AsNoTracking()
            .Include(c => c.Paciente).ThenInclude(p => p!.Usuario)
            .Where(c => c.DoctorId == id)
            .OrderByDescending(c => c.FechaHora)
            .Select(c => new
            {
                tipo      = "cita",
                fecha     = c.FechaHora,
                c.CitaId,
                c.Estado,
                c.Motivo,
                c.DuracionMinutos,
                paciente  = c.Paciente == null ? null : new
                {
                    nombre   = c.Paciente.Usuario!.Nombre,
                    apellido = c.Paciente.Usuario.Apellido
                }
            })
            .ToListAsync();

        // Recetas emitidas por este doctor
        var recetas = await _db.Recetas
            .AsNoTracking()
            .Include(r => r.Paciente).ThenInclude(p => p!.Usuario)
            .Include(r => r.Detalles).ThenInclude(d => d.Medicamento)
            .Where(r => r.DoctorId == id)
            .OrderByDescending(r => r.FechaEmision)
            .Select(r => new
            {
                tipo          = "receta",
                fecha         = r.FechaEmision,
                r.RecetaId,
                r.Observaciones,
                paciente = r.Paciente == null ? null : new
                {
                    nombre   = r.Paciente.Usuario!.Nombre,
                    apellido = r.Paciente.Usuario.Apellido
                },
                medicamentos = r.Detalles.Select(d => new
                {
                    nombre    = d.Medicamento!.Nombre,
                    d.Dosis,
                    d.Frecuencia,
                    d.Duracion,
                    d.Cantidad
                })
            })
            .ToListAsync();

        // Pagos de citas atendidas por este doctor
        var citaIds = await _db.Citas.AsNoTracking()
            .Where(c => c.DoctorId == id && c.Estado == "Completada")
            .Select(c => c.CitaId)
            .ToListAsync();

        var pagos = citaIds.Count > 0
            ? await _db.Pagos.AsNoTracking()
                .Include(p => p.MetodoPago)
                .Where(p => p.CitaId.HasValue && citaIds.Contains(p.CitaId.Value))
                .OrderByDescending(p => p.FechaPago)
                .Select(p => new
                {
                    tipo     = "pago",
                    fecha    = p.FechaPago,
                    p.PagoId,
                    p.Monto,
                    p.Impuesto,
                    p.Total,
                    p.Estado,
                    metodo   = p.MetodoPago!.Nombre
                })
                .ToListAsync()
            : new List<object>() as dynamic;

        return Ok(new
        {
            citas,
            recetas,
            pagos,
            resumen = new
            {
                totalCitas       = citas.Count,
                citasCompletadas = citas.Count(c => c.Estado == "Completada"),
                totalRecetas     = recetas.Count,
                totalPacientes   = citas.Select(c => c.paciente).Distinct().Count()
            }
        });
    }
}

public class ActualizarEstadoDto
{
    public string Estado { get; set; } = "Disponible";
    public string? Observacion { get; set; }
}