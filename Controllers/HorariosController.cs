using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClinicaAPI.Constants;
using ClinicaAPI.Data;
using ClinicaAPI.Models;

namespace ClinicaAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HorariosController : ControllerBase
{
    private readonly ClinicaContext _db;
    public HorariosController(ClinicaContext db) => _db = db;

    // ── GET /api/horarios/doctor/{doctorId} ────────────────────
    // Devuelve los horarios semanales configurados del doctor
    [HttpGet("doctor/{doctorId}")]
    public async Task<IActionResult> GetHorarios(int doctorId) =>
        Ok(await _db.DoctorHorarios
            .Where(h => h.DoctorId == doctorId && h.Activo)
            .OrderBy(h => h.DiaSemana).ThenBy(h => h.HoraInicio)
            .ToListAsync());

    // ── POST /api/horarios ─────────────────────────────────────
    // Admin define o actualiza el horario semanal de un doctor
    [HttpPost]
    [Authorize(Roles = AuthRoles.SoloAdmin)]
    public async Task<IActionResult> SetHorario([FromBody] DoctorHorario dto)
    {
        // Si ya existe ese día para ese doctor, reemplazar
        var existente = await _db.DoctorHorarios
            .FirstOrDefaultAsync(h => h.DoctorId == dto.DoctorId && h.DiaSemana == dto.DiaSemana);

        if (existente != null)
        {
            existente.HoraInicio       = dto.HoraInicio;
            existente.HoraFin          = dto.HoraFin;
            existente.DuracionSlotMin  = dto.DuracionSlotMin;
            existente.Activo           = true;
        }
        else
        {
            _db.DoctorHorarios.Add(dto);
        }

        await _db.SaveChangesAsync();
        return Ok(dto);
    }

    // ── DELETE /api/horarios/{id} ──────────────────────────────
    [HttpDelete("{id}")]
    [Authorize(Roles = AuthRoles.SoloAdmin)]
    public async Task<IActionResult> DeleteHorario(int id)
    {
        var h = await _db.DoctorHorarios.FindAsync(id);
        if (h == null) return NotFound();
        h.Activo = false;
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ── GET /api/horarios/slots/{doctorId}?fecha=2026-04-21 ────
    // Devuelve los slots disponibles (libres) del doctor en una fecha concreta.
    // La lógica:
    //   1. Obtener horario del doctor para ese día de semana.
    //   2. Generar todos los slots del día (cada DuracionSlotMin minutos).
    //   3. Eliminar los slots que ya tienen una cita (no cancelada).
    //   4. Eliminar si hay un bloqueo que cubre ese slot.
    //   5. No generar sábados/domingos si el doctor no trabaja esos días.
    [HttpGet("slots/{doctorId}")]
    public async Task<IActionResult> GetSlots(int doctorId, [FromQuery] DateOnly fecha)
    {
        // Rechazar sábados (6) y domingos (0) de entrada — el frontend igual los filtra
        var diaSemana = (int)fecha.DayOfWeek;

        var horario = await _db.DoctorHorarios
            .FirstOrDefaultAsync(h => h.DoctorId == doctorId
                                   && h.DiaSemana == diaSemana
                                   && h.Activo);

        if (horario == null)
            return Ok(new { disponibles = Array.Empty<object>(), mensaje = "El doctor no trabaja ese día." });

        // Citas ocupadas ese día (no canceladas)
        var fechaDatetime = fecha.ToDateTime(TimeOnly.MinValue);
        var citasOcupadas = await _db.Citas
            .Where(c => c.DoctorId == doctorId
                     && c.FechaHora.Date == fechaDatetime.Date
                     && c.Estado != "Cancelada")
            .Select(c => new { c.FechaHora, c.FechaFin })
            .ToListAsync();

        // Bloqueos ese día
        var bloqueos = await _db.BloqueosHorario
            .Where(b => b.DoctorId == doctorId && b.Fecha == fecha)
            .ToListAsync();

        // Bloqueo de día completo
        if (bloqueos.Any(b => b.HoraInicio == null))
            return Ok(new { disponibles = Array.Empty<object>(), mensaje = "El doctor tiene el día bloqueado." });

        // Generar slots
        var slots = new List<object>();
        var cursor = horario.HoraInicio;

        while (cursor.AddMinutes(horario.DuracionSlotMin) <= horario.HoraFin)
        {
            var slotInicio = fecha.ToDateTime(cursor);
            var slotFin    = slotInicio.AddMinutes(horario.DuracionSlotMin);

            // ¿Ocupado por cita?
            bool citaConflicto = citasOcupadas.Any(c =>
                c.FechaHora < slotFin && c.FechaFin > slotInicio);

            // ¿Bloqueado manualmente?
            bool bloqueado = bloqueos.Any(b =>
                b.HoraInicio.HasValue && b.HoraFin.HasValue &&
                b.HoraInicio.Value.ToTimeSpan() < slotFin.TimeOfDay &&
                b.HoraFin.Value.ToTimeSpan()    > slotInicio.TimeOfDay);

            // No mostrar slots pasados si es hoy
            bool pasado = slotInicio <= DateTime.Now;

            slots.Add(new
            {
                inicio       = slotInicio,
                fin          = slotFin,
                disponible   = !citaConflicto && !bloqueado && !pasado,
                motivo       = citaConflicto ? "ocupado" : bloqueado ? "bloqueado" : pasado ? "pasado" : null
            });

            cursor = cursor.AddMinutes(horario.DuracionSlotMin);
        }

        return Ok(new { disponibles = slots, duracionSlotMin = horario.DuracionSlotMin });
    }

    // ── POST /api/horarios/bloqueos ────────────────────────────
    [HttpPost("bloqueos")]
    [Authorize(Roles = AuthRoles.SoloAdmin)]
    public async Task<IActionResult> AddBloqueo([FromBody] BloqueoHorario dto)
    {
        _db.BloqueosHorario.Add(dto);
        await _db.SaveChangesAsync();
        return Ok(dto);
    }

    // ── GET /api/horarios/bloqueos/doctor/{doctorId} ───────────
    [HttpGet("bloqueos/doctor/{doctorId}")]
    public async Task<IActionResult> GetBloqueos(int doctorId) =>
        Ok(await _db.BloqueosHorario
            .Where(b => b.DoctorId == doctorId && b.Fecha >= DateOnly.FromDateTime(DateTime.Today))
            .OrderBy(b => b.Fecha)
            .ToListAsync());

    // ── DELETE /api/horarios/bloqueos/{id} ─────────────────────
    [HttpDelete("bloqueos/{id}")]
    [Authorize(Roles = AuthRoles.SoloAdmin)]
    public async Task<IActionResult> DeleteBloqueo(int id)
    {
        var b = await _db.BloqueosHorario.FindAsync(id);
        if (b == null) return NotFound();
        _db.BloqueosHorario.Remove(b);
        await _db.SaveChangesAsync();
        return Ok();
    }
}