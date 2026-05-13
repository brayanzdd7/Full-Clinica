// ══════════════════════════════════════════════════════════════════════
//  PagosController.cs — agrega notificación de correo al registrar pago
// ══════════════════════════════════════════════════════════════════════
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
public class PagosController : ControllerBase
{
    private const decimal IVA = 0.12m;
    private readonly ClinicaContext _db;
    private readonly IAccessControlService _access;
    private readonly IEmailService _email;

    public PagosController(ClinicaContext db, IAccessControlService access, IEmailService email)
    {
        _db     = db;
        _access = access;
        _email  = email;
    }

    [HttpGet]
    [Authorize(Roles = AuthRoles.AdminYRecepcion)]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Pagos
            .Include(p => p.Cita)
                .ThenInclude(c => c!.Paciente).ThenInclude(p => p!.Usuario)
            .Include(p => p.Cita)
                .ThenInclude(c => c!.Doctor).ThenInclude(d => d!.Usuario)
            .Include(p => p.VentaDirecta)
                .ThenInclude(v => v!.Paciente).ThenInclude(p => p!.Usuario)
            .Include(p => p.MetodoPago)
            .OrderByDescending(p => p.FechaPago)
            .ToListAsync());

    [HttpGet("{id}")]
    [Authorize(Roles = AuthRoles.AdminYRecepcion)]
    public async Task<IActionResult> GetById(int id)
    {
        var pago = await _db.Pagos
            .Include(p => p.MetodoPago)
            .Include(p => p.Cita)
                .ThenInclude(c => c!.Paciente).ThenInclude(p => p!.Usuario)
            .Include(p => p.Cita)
                .ThenInclude(c => c!.Doctor).ThenInclude(d => d!.Usuario)
            .Include(p => p.VentaDirecta)
                .ThenInclude(v => v!.Paciente).ThenInclude(p => p!.Usuario)
            .FirstOrDefaultAsync(p => p.PagoId == id);
        return pago == null ? NotFound() : Ok(pago);
    }

    [HttpGet("paciente/{pacienteId}")]
    public async Task<IActionResult> GetByPaciente(int pacienteId)
    {
        if (!await _access.PuedeVerPacienteAsync(pacienteId)) return Forbid();
        var citaIds = await _db.Citas.AsNoTracking()
            .Where(c => c.PacienteId == pacienteId).Select(c => c.CitaId).ToListAsync();
        var pagosCita = citaIds.Count > 0
            ? await _db.Pagos.AsNoTracking()
                .Where(p => p.CitaId.HasValue && citaIds.Contains(p.CitaId.Value))
                .Include(p => p.MetodoPago).OrderByDescending(p => p.FechaPago).ToListAsync()
            : new();
        var ventaIds = await _db.VentasDirectas.AsNoTracking()
            .Where(v => v.PacienteId == pacienteId).Select(v => v.VentaId).ToListAsync();
        var pagosVenta = ventaIds.Count > 0
            ? await _db.Pagos.AsNoTracking()
                .Where(p => p.VentaDirectaId.HasValue && ventaIds.Contains(p.VentaDirectaId.Value))
                .Include(p => p.MetodoPago).OrderByDescending(p => p.FechaPago).ToListAsync()
            : new();
        return Ok(pagosCita.Concat(pagosVenta).OrderByDescending(p => p.FechaPago));
    }

    [HttpPost]
    [Authorize(Roles = AuthRoles.AdminYRecepcion)]
    public async Task<IActionResult> Create([FromBody] RegistrarPagoDto dto)
    {
        if (dto.CitaId.HasValue)
        {
            var cita = await _db.Citas.FindAsync(dto.CitaId.Value);
            if (cita == null) return NotFound("Cita no encontrada.");
            if (await _db.Pagos.AnyAsync(p => p.CitaId == dto.CitaId.Value))
                return BadRequest("Esta cita ya tiene un pago registrado.");
        }

        var impuesto = Math.Round(dto.Monto * IVA, 2);
        var total    = dto.Monto + impuesto;

        var pago = new Pago
        {
            CitaId         = dto.CitaId,
            VentaDirectaId = null,
            MetodoPagoId   = dto.MetodoPagoId,
            Monto          = dto.Monto,
            Impuesto       = impuesto,
            Total          = total,
            FechaPago      = DateTime.Now,
            Estado         = "Completado"
        };
        _db.Pagos.Add(pago);
        await _db.SaveChangesAsync();

        // ── Correo al paciente ─────────────────────────────────────
        _ = Task.Run(async () =>
        {
            try
            {
                string emailPac = "", nomPac = "", concepto = "Pago", metodoPago = "";

                var metodo = await _db.MetodosPago.FindAsync(dto.MetodoPagoId);
                metodoPago = metodo?.Nombre ?? "—";

                if (dto.CitaId.HasValue)
                {
                    var citaDatos = await _db.Citas
                        .Include(c => c.Paciente).ThenInclude(p => p!.Usuario)
                        .FirstOrDefaultAsync(c => c.CitaId == dto.CitaId.Value);
                    emailPac = citaDatos?.Paciente?.Usuario?.Email ?? "";
                    nomPac   = $"{citaDatos?.Paciente?.Usuario?.Nombre} {citaDatos?.Paciente?.Usuario?.Apellido}".Trim();
                    concepto = "Consulta médica";
                }

                if (string.IsNullOrEmpty(emailPac)) return;

                await _email.SendPagoRegistradoAsync(
                    emailPac, nomPac, dto.Monto, impuesto, total, metodoPago, concepto);
            }
            catch { }
        });

        return Ok(new { pago.PagoId, pago.Monto, pago.Impuesto, pago.Total, pago.Estado });
    }

    [HttpGet("metodos")]
    public async Task<IActionResult> GetMetodos() =>
        Ok(await _db.MetodosPago.ToListAsync());

    [HttpGet("resumen")]
    [Authorize(Roles = AuthRoles.SoloAdmin)]
    public async Task<IActionResult> Resumen()
    {
        var hoy = DateTime.Today;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var pagos = await _db.Pagos.Include(p => p.MetodoPago).ToListAsync();
        return Ok(new
        {
            totalHoy     = pagos.Where(p => p.FechaPago.Date == hoy).Sum(p => p.Total),
            totalMes     = pagos.Where(p => p.FechaPago >= inicioMes).Sum(p => p.Total),
            totalGeneral = pagos.Sum(p => p.Total),
            countHoy     = pagos.Count(p => p.FechaPago.Date == hoy),
            porMetodo    = pagos.GroupBy(p => p.MetodoPago?.Nombre ?? "Sin método")
                .Select(g => new { metodo = g.Key, total = g.Sum(p => p.Total), count = g.Count() })
        });
    }
}

public class RegistrarPagoDto
{
    public int? CitaId { get; set; }
    public int MetodoPagoId { get; set; }
    public decimal Monto { get; set; }
}