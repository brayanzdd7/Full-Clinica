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
public class CitasController : ControllerBase
{
    private readonly ClinicaContext _db;
    private readonly IAccessControlService _access;
    private readonly IEmailService _email;

    public CitasController(ClinicaContext db, IAccessControlService access, IEmailService email)
    {
        _db     = db;
        _access = access;
        _email  = email;
    }

    // ── GET /api/citas ─────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = AuthRoles.AdminYRecepcion)]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Citas
            .Include(c => c.Doctor).ThenInclude(d => d!.Usuario)
            .Include(c => c.Paciente).ThenInclude(p => p!.Usuario)
            .OrderByDescending(c => c.FechaHora)
            .ToListAsync());

    // ── GET /api/citas/{id} ────────────────────────────────────
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!await _access.PuedeAccederCitaAsync(id)) return Forbid();
        var cita = await _db.Citas
            .Include(c => c.Doctor).ThenInclude(d => d!.Usuario)
            .Include(c => c.Paciente).ThenInclude(p => p!.Usuario)
            .FirstOrDefaultAsync(c => c.CitaId == id);
        return cita == null ? NotFound() : Ok(cita);
    }

    // ── GET /api/citas/doctor/{doctorId} ───────────────────────
    [HttpGet("doctor/{doctorId}")]
    public async Task<IActionResult> GetByDoctor(int doctorId)
    {
        if (!await _access.PuedeGestionarDoctorAsync(doctorId)) return Forbid();
        return Ok(await _db.Citas
            .Include(c => c.Paciente).ThenInclude(p => p!.Usuario)
            .Where(c => c.DoctorId == doctorId)
            .OrderBy(c => c.FechaHora)
            .ToListAsync());
    }

    // ── GET /api/citas/paciente/{pacienteId} ───────────────────
    [HttpGet("paciente/{pacienteId}")]
    public async Task<IActionResult> GetByPaciente(int pacienteId)
    {
        if (!await _access.PuedeVerPacienteAsync(pacienteId)) return Forbid();
        return Ok(await _db.Citas
            .Include(c => c.Doctor).ThenInclude(d => d!.Usuario)
            .Include(c => c.Doctor).ThenInclude(d => d!.Especialidad)
            .Where(c => c.PacienteId == pacienteId)
            .OrderByDescending(c => c.FechaHora)
            .ToListAsync());
    }

    // ── POST /api/citas ────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearCitaDto dto)
    {
        var diaSemana = (int)dto.FechaHora.DayOfWeek;
        var fechaOnly = DateOnly.FromDateTime(dto.FechaHora);

        var horario = await _db.DoctorHorarios
            .FirstOrDefaultAsync(h => h.DoctorId == dto.DoctorId
                                   && h.DiaSemana == diaSemana && h.Activo);
        if (horario == null)
            return BadRequest($"El doctor no trabaja el {dto.FechaHora.DayOfWeek} ({dto.FechaHora:dd/MM/yyyy}).");

        var horaSlot = TimeOnly.FromDateTime(dto.FechaHora);
        if (horaSlot < horario.HoraInicio || horaSlot.AddMinutes(horario.DuracionSlotMin) > horario.HoraFin)
            return BadRequest($"Hora fuera del horario del doctor ({horario.HoraInicio} – {horario.HoraFin}).");

        var bloqueoDia = await _db.BloqueosHorario.AnyAsync(b =>
            b.DoctorId == dto.DoctorId && b.Fecha == fechaOnly &&
            (b.HoraInicio == null || (b.HoraInicio.Value <= horaSlot && b.HoraFin!.Value > horaSlot)));
        if (bloqueoDia) return BadRequest("El doctor tiene ese horario bloqueado.");

        var fechaFin = dto.FechaHora.AddMinutes(horario.DuracionSlotMin);

        var solapamientoDoc = await _db.Citas.AnyAsync(c =>
            c.DoctorId == dto.DoctorId && c.Estado != "Cancelada" &&
            c.FechaHora < fechaFin && c.FechaFin > dto.FechaHora);
        if (solapamientoDoc) return BadRequest("El doctor ya tiene una cita en ese horario.");

        var solapamientoPac = await _db.Citas.AnyAsync(c =>
            c.PacienteId == dto.PacienteId && c.Estado != "Cancelada" &&
            c.FechaHora < fechaFin && c.FechaFin > dto.FechaHora);
        if (solapamientoPac) return BadRequest("El paciente ya tiene una cita en ese horario.");

        var cita = new Cita
        {
            PacienteId      = dto.PacienteId,
            DoctorId        = dto.DoctorId,
            FechaHora       = dto.FechaHora,
            FechaFin        = fechaFin,
            DuracionMinutos = horario.DuracionSlotMin,
            Motivo          = dto.Motivo,
            Estado          = "Pendiente",
            FechaCreacion   = DateTime.Now
        };
        _db.Citas.Add(cita);
        await _db.SaveChangesAsync();

        // ── Correos de notificación ───────────────────────────────
        _ = Task.Run(async () =>
        {
            try
            {
                var citaConDatos = await _db.Citas
                    .Include(c => c.Doctor).ThenInclude(d => d!.Usuario)
                    .Include(c => c.Paciente).ThenInclude(p => p!.Usuario)
                    .FirstOrDefaultAsync(c => c.CitaId == cita.CitaId);

                if (citaConDatos == null) return;

                var emailPac  = citaConDatos.Paciente?.Usuario?.Email ?? "";
                var emailDoc  = citaConDatos.Doctor?.Usuario?.Email   ?? "";
                var nomPac    = $"{citaConDatos.Paciente?.Usuario?.Nombre} {citaConDatos.Paciente?.Usuario?.Apellido}".Trim();
                var nomDoc    = $"Dr. {citaConDatos.Doctor?.Usuario?.Nombre} {citaConDatos.Doctor?.Usuario?.Apellido}".Trim();

                await _email.SendCitaAgendadaAsync(
                    emailPac, emailDoc, nomPac, nomDoc, cita.FechaHora, dto.Motivo);
            }
            catch { /* no interrumpir si el correo falla */ }
        });

        return Ok(cita);
    }

    // ── PUT /api/citas/{id}/estado ─────────────────────────────
    [HttpPut("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] string estado)
    {
        if (!await _access.PuedeAccederCitaAsync(id)) return Forbid();

        var cita = await _db.Citas
            .Include(c => c.Doctor).ThenInclude(d => d!.Usuario)
            .Include(c => c.Paciente).ThenInclude(p => p!.Usuario)
            .FirstOrDefaultAsync(c => c.CitaId == id);
        if (cita == null) return NotFound();

        cita.Estado = estado;

        var estadoMedico = await _db.EstadoMedico.FirstOrDefaultAsync(e => e.DoctorId == cita.DoctorId);
        if (estadoMedico != null)
        {
            estadoMedico.Estado = estado == "En Curso" ? "En Consulta" : "Disponible";
            estadoMedico.CitaActualId = estado == "En Curso" ? id : null;
            estadoMedico.FechaHoraActualizacion = DateTime.Now;
        }

        await _db.SaveChangesAsync();

        // ── Correo de cambio de estado ────────────────────────────
        if (estado is "Confirmada" or "Cancelada" or "Completada")
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var emailPac = cita.Paciente?.Usuario?.Email ?? "";
                    var emailDoc = cita.Doctor?.Usuario?.Email   ?? "";
                    var nomPac   = $"{cita.Paciente?.Usuario?.Nombre} {cita.Paciente?.Usuario?.Apellido}".Trim();
                    var nomDoc   = $"Dr. {cita.Doctor?.Usuario?.Nombre} {cita.Doctor?.Usuario?.Apellido}".Trim();

                    await _email.SendCitaCambioEstadoAsync(
                        emailPac, emailDoc, nomPac, nomDoc, cita.FechaHora, estado);
                }
                catch { }
            });
        }

        return Ok(cita);
    }

    // ── DELETE /api/citas/{id} ─────────────────────────────────
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _access.PuedeAccederCitaAsync(id)) return Forbid();
        var cita = await _db.Citas
            .Include(c => c.Doctor).ThenInclude(d => d!.Usuario)
            .Include(c => c.Paciente).ThenInclude(p => p!.Usuario)
            .FirstOrDefaultAsync(c => c.CitaId == id);
        if (cita == null) return NotFound();
        cita.Estado = "Cancelada";
        await _db.SaveChangesAsync();

        _ = Task.Run(async () =>
        {
            try
            {
                var emailPac = cita.Paciente?.Usuario?.Email ?? "";
                var emailDoc = cita.Doctor?.Usuario?.Email   ?? "";
                var nomPac   = $"{cita.Paciente?.Usuario?.Nombre} {cita.Paciente?.Usuario?.Apellido}".Trim();
                var nomDoc   = $"Dr. {cita.Doctor?.Usuario?.Nombre} {cita.Doctor?.Usuario?.Apellido}".Trim();
                await _email.SendCitaCambioEstadoAsync(emailPac, emailDoc, nomPac, nomDoc, cita.FechaHora, "Cancelada");
            }
            catch { }
        });

        return Ok();
    }
}

public class CrearCitaDto
{
    public int PacienteId { get; set; }
    public int DoctorId   { get; set; }
    public DateTime FechaHora { get; set; }
    public string? Motivo { get; set; }
}