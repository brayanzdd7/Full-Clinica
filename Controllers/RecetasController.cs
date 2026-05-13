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
public class RecetasController : ControllerBase
{
    private readonly ClinicaContext _context;
    private readonly IAccessControlService _access;

    public RecetasController(ClinicaContext context, IAccessControlService access)
    {
        _context = context;
        _access  = access;
    }

    /// <summary>Admin y recepción: listado reciente de recetas (farmacia / cobro).</summary>
    [HttpGet]
    [Authorize(Roles = AuthRoles.AdminYRecepcion)]
    public async Task<IActionResult> GetListaStaff()
    {
        var lista = await _context.Recetas
            .AsNoTracking()
            .Include(r => r.Doctor).ThenInclude(d => d!.Usuario)
            .Include(r => r.Paciente).ThenInclude(p => p!.Usuario)
            .Include(r => r.Cita)
            .Include(r => r.Detalles).ThenInclude(d => d.Medicamento)
            .OrderByDescending(r => r.FechaEmision)
            .Take(300)
            .ToListAsync();
        return Ok(lista);
    }

    [HttpGet("paciente/{pacienteId}")]
    public async Task<IActionResult> GetByPaciente(int pacienteId)
    {
        if (!await _access.PuedeVerPacienteAsync(pacienteId))
            return Forbid();

        var lista = await _context.Recetas
            .AsNoTracking()
            .Include(r => r.Doctor).ThenInclude(d => d!.Usuario)
            .Include(r => r.Detalles).ThenInclude(d => d.Medicamento)
            .Where(r => r.PacienteId == pacienteId)
            .OrderByDescending(r => r.FechaEmision)
            .ToListAsync();
        return Ok(lista);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearRecetaDto dto)
    {
        // Medicamentos son OPCIONALES — la receta puede tener solo observaciones
        if (dto.Lineas == null)
            dto.Lineas = new List<LineaRecetaDto>();

        // Requiere al menos observaciones O al menos una línea
        if (dto.Lineas.Count == 0 && string.IsNullOrWhiteSpace(dto.Observaciones))
            return BadRequest("Agrega al menos un medicamento o escribe una indicación en observaciones.");

        var cita = await _context.Citas.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CitaId == dto.CitaId);
        if (cita == null)
            return NotFound("Cita no encontrada.");

        if (cita.Estado != "Completada")
            return BadRequest("Solo se puede emitir receta en citas completadas.");

        var usuario = await _access.GetUsuarioActualAsync();
        var rol     = usuario?.Rol?.Nombre;
        if (rol == null)
            return Unauthorized();

        if (rol == RoleNames.Doctor)
        {
            var miDoctor = await _access.GetDoctorIdActualAsync();
            if (miDoctor != cita.DoctorId)
                return Forbid();
        }
        else if (rol != RoleNames.Admin && rol != "Administrador")
            return Forbid();

        var receta = new Receta
        {
            CitaId        = dto.CitaId,
            DoctorId      = cita.DoctorId,
            PacienteId    = cita.PacienteId,
            Observaciones = dto.Observaciones,
            FechaEmision  = DateTime.Now,

            // ── Cambio 2: MedicamentoId nullable + NombreLibre ────────────
            Detalles = dto.Lineas.Select(l => new DetalleReceta
            {
                MedicamentoId = l.MedicamentoId > 0 ? l.MedicamentoId : (int?)null,
                NombreLibre   = l.NombreLibre,
                Dosis         = l.Dosis,
                Frecuencia    = l.Frecuencia,
                Duracion      = l.Duracion,
                Cantidad      = l.Cantidad
            }).ToList()
        };

        _context.Recetas.Add(receta);
        await _context.SaveChangesAsync();

        // ── Cambio 4: descontar stock solo cuando hay MedicamentoId ───────
        foreach (var detalle in receta.Detalles)
        {
            if (detalle.MedicamentoId.HasValue)
            {
                var med = await _context.Medicamentos.FindAsync(detalle.MedicamentoId.Value);
                if (med != null)
                {
                    med.Stock -= detalle.Cantidad;
                    if (med.Stock < 0) med.Stock = 0;
                }
            }
        }

        await _context.SaveChangesAsync();

        var completa = await _context.Recetas
            .AsNoTracking()
            .Include(r => r.Doctor).ThenInclude(d => d!.Usuario)
            .Include(r => r.Detalles).ThenInclude(d => d.Medicamento)
            .FirstAsync(r => r.RecetaId == receta.RecetaId);

        return Ok(completa);
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class CrearRecetaDto
{
    public int    CitaId        { get; set; }
    public string? Observaciones { get; set; }
    public List<LineaRecetaDto> Lineas { get; set; } = new();
}

public class LineaRecetaDto
{
    /// <summary>
    /// Texto libre cuando el medicamento no está en el catálogo.
    /// Se usa cuando MedicamentoId es 0 o no se envía.
    /// </summary>
    public string? NombreLibre   { get; set; }   // ← Cambio 1

    public int     MedicamentoId { get; set; }
    public string? Dosis         { get; set; }
    public string? Frecuencia    { get; set; }
    public string? Duracion      { get; set; }
    public int     Cantidad      { get; set; }
}