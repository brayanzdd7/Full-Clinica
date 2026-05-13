using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClinicaAPI.Data;
using ClinicaAPI.Services;

namespace ClinicaAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ClinicaContext _db;
    private readonly IAccessControlService _access;

    public DashboardController(ClinicaContext db, IAccessControlService access)
    {
        _db = db;
        _access = access;
    }

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen()
    {
        var hoy       = DateTime.Today;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);

        var usuario = await _access.GetUsuarioActualAsync();
        var rol = usuario?.Rol?.Nombre ?? "";

        // ── Vista Doctor ───────────────────────────────────────
        if (rol == "Doctor")
        {
            var doctorId = await _access.GetDoctorIdActualAsync();
            if (doctorId == null) return Unauthorized();

            var citas = await _db.Citas
                .Include(c => c.Paciente).ThenInclude(p => p!.Usuario)
                .Where(c => c.DoctorId == doctorId)
                .OrderBy(c => c.FechaHora)
                .ToListAsync();

            var citasHoy        = citas.Where(c => c.FechaHora.Date == hoy).ToList();
            var completadasMes  = citas.Count(c => c.Estado == "Completada" && c.FechaHora >= inicioMes);
            var pacientesUnicos = citas.Select(c => c.PacienteId).Distinct().Count();

            // Recetas emitidas este mes — FIXED: antes siempre devolvía 0
            var recetasMes = await _db.Recetas
                .CountAsync(r => r.DoctorId == doctorId && r.FechaEmision >= inicioMes);

            // Recetas recientes (últimas 5) para mostrar en el dashboard
            var recetasRecientes = await _db.Recetas
                .AsNoTracking()
                .Include(r => r.Paciente).ThenInclude(p => p!.Usuario)
                .Include(r => r.Detalles).ThenInclude(d => d.Medicamento)
                .Where(r => r.DoctorId == doctorId)
                .OrderByDescending(r => r.FechaEmision)
                .Take(5)
                .Select(r => new
                {
                    r.RecetaId,
                    r.FechaEmision,
                    r.Observaciones,
                    paciente = r.Paciente == null ? null : new
                    {
                        nombre   = r.Paciente.Usuario!.Nombre,
                        apellido = r.Paciente.Usuario.Apellido
                    },
                    medicamentos = r.Detalles.Select(d => d.Medicamento!.Nombre)
                })
                .ToListAsync();

            return Ok(new
            {
                stats = new
                {
                    citasHoy        = citasHoy.Count,
                    completadasMes,
                    pacientesUnicos,
                    recetasMes
                },
                citasHoy = citasHoy.Select(c => new
                {
                    c.CitaId,
                    c.FechaHora,
                    c.FechaFin,
                    c.DuracionMinutos,
                    c.Estado,
                    c.Motivo,
                    paciente = c.Paciente?.Usuario == null ? null : new
                    {
                        nombre   = c.Paciente.Usuario.Nombre,
                        apellido = c.Paciente.Usuario.Apellido
                    }
                }),
                recetasRecientes
            });
        }

        // ── Vista Admin / Recepcionista ────────────────────────
        var citasAll = await _db.Citas
            .Include(c => c.Doctor).ThenInclude(d => d!.Usuario)
            .Include(c => c.Paciente).ThenInclude(p => p!.Usuario)
            .OrderByDescending(c => c.FechaHora)
            .Take(50)
            .ToListAsync();

        var doctores = await _db.Doctores
            .Include(d => d.Usuario)
            .Include(d => d.Especialidad)
            .Include(d => d.EstadoMedico)
            .Take(8)
            .ToListAsync();

        var totalPacientes    = await _db.Pacientes.CountAsync();
        var totalDoctores     = await _db.Doctores.CountAsync();
        var totalMedicamentos = await _db.Medicamentos.CountAsync();
        var medsStockBajo     = await _db.Medicamentos
            .Where(m => m.Stock < 10).Take(10).ToListAsync();

        decimal ingresosMes = await _db.Facturas
            .Where(f => f.FechaEmision >= inicioMes && f.Estado == "Pagada")
            .SumAsync(f => f.Total);

        var citasHoyCount = citasAll.Count(c => c.FechaHora.Date == hoy);

        return Ok(new
        {
            stats = new
            {
                citasHoy      = citasHoyCount,
                totalPacientes,
                totalDoctores,
                totalMedicamentos,
                medsStockBajo = medsStockBajo.Count,
                ingresosMes
            },
            citasRecientes = citasAll.Take(6).Select(c => new
            {
                c.CitaId, c.FechaHora, c.Estado, c.Motivo,
                paciente = c.Paciente?.Usuario == null ? null : new
                {
                    nombre   = c.Paciente.Usuario.Nombre,
                    apellido = c.Paciente.Usuario.Apellido
                },
                doctor = c.Doctor?.Usuario == null ? null : new
                {
                    nombre   = c.Doctor.Usuario.Nombre,
                    apellido = c.Doctor.Usuario.Apellido
                }
            }),
            doctores = doctores.Select(d => new
            {
                d.DoctorId,
                usuario      = d.Usuario == null ? null : new { d.Usuario.Nombre, d.Usuario.Apellido },
                especialidad = d.Especialidad == null ? null : new { d.Especialidad.Nombre },
                estadoMedico = d.EstadoMedico == null ? null : new { d.EstadoMedico.Estado }
            }),
            medsStockBajo = medsStockBajo.Select(m => new
            {
                m.MedicamentoId, m.Nombre, m.Stock, m.Presentacion, m.FechaVencimiento
            })
        });
    }
}