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
public class FacturasController : ControllerBase
{
    private readonly ClinicaContext _db;
    private readonly IAccessControlService _access;
    private readonly IEmailService _email;

    public FacturasController(ClinicaContext db, IAccessControlService access, IEmailService email)
    {
        _db     = db;
        _access = access;
        _email  = email;
    }

    [HttpGet]
    [Authorize(Roles = AuthRoles.AdminYRecepcion)]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Facturas
            .Include(f => f.Paciente).ThenInclude(p => p!.Usuario)
            .Include(f => f.Pago).ThenInclude(p => p!.MetodoPago)
            .OrderByDescending(f => f.FechaEmision)
            .ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!await _access.PuedeAccederFacturaAsync(id)) return Forbid();
        var f = await _db.Facturas
            .Include(f => f.Cita).ThenInclude(c => c!.Doctor).ThenInclude(d => d!.Usuario)
            .Include(f => f.Cita).ThenInclude(c => c!.Paciente).ThenInclude(p => p!.Usuario)
            .Include(f => f.Paciente).ThenInclude(p => p!.Usuario)
            .Include(f => f.Pago).ThenInclude(p => p!.MetodoPago)
            .Include(f => f.Pago).ThenInclude(p => p!.VentaDirecta)
                .ThenInclude(v => v!.Paciente).ThenInclude(p => p!.Usuario)
            .FirstOrDefaultAsync(f => f.FacturaId == id);
        return f == null ? NotFound() : Ok(f);
    }

    [HttpGet("paciente/{pacienteId}")]
    public async Task<IActionResult> GetByPaciente(int pacienteId)
    {
        if (!await _access.PuedeVerPacienteAsync(pacienteId)) return Forbid();
        return Ok(await _db.Facturas
            .Include(f => f.Pago).ThenInclude(p => p!.MetodoPago)
            .Where(f => f.PacienteId == pacienteId)
            .OrderByDescending(f => f.FechaEmision)
            .ToListAsync());
    }

    [HttpPost]
    [Authorize(Roles = AuthRoles.AdminYRecepcion)]
    public async Task<IActionResult> Create([FromBody] CrearFacturaDto dto)
    {
        var pago = await _db.Pagos
            .Include(p => p.MetodoPago)
            .Include(p => p.VentaDirecta).ThenInclude(v => v!.Paciente).ThenInclude(p => p!.Usuario)
            .FirstOrDefaultAsync(p => p.PagoId == dto.PagoId);

        if (pago == null)
            return BadRequest("Registra el pago primero antes de emitir la factura.");

        if (await _db.Facturas.AnyAsync(f => f.PagoId == dto.PagoId))
            return BadRequest("Este pago ya tiene una factura emitida.");

        bool esCita = dto.CitaId.HasValue && dto.CitaId.Value > 0;
        if (esCita && await _db.Facturas.AnyAsync(f => f.CitaId == dto.CitaId!.Value))
            return BadRequest("Esta cita ya fue facturada.");

        int pacienteId = dto.PacienteId;
        if (pacienteId == 0)
        {
            if (pago.VentaDirectaId.HasValue && pago.VentaDirecta != null)
                pacienteId = pago.VentaDirecta.PacienteId;
            else if (esCita)
            {
                var cita = await _db.Citas.FindAsync(dto.CitaId!.Value);
                pacienteId = cita?.PacienteId ?? 0;
            }
        }
        if (pacienteId == 0)
            return BadRequest("No se pudo determinar el paciente. Verifica el pago.");

        var year   = DateTime.Now.Year;
        var count  = await _db.Facturas.CountAsync(f => f.FechaEmision.Year == year);
        var numero = $"FAC-{year}-{(count + 1):D4}";

        var concepto = string.IsNullOrWhiteSpace(dto.Concepto)
            ? (pago.VentaDirectaId.HasValue ? "Venta de medicamentos" : "Consulta médica")
            : dto.Concepto;

        var factura = new Factura
        {
            NumeroFactura = numero,
            CitaId        = esCita ? dto.CitaId!.Value : null,
            PacienteId    = pacienteId,
            PagoId        = dto.PagoId,
            FechaEmision  = DateTime.Now,
            Concepto      = concepto,
            Subtotal      = pago.Monto,
            Impuesto      = pago.Impuesto,
            Total         = pago.Total,
            Observaciones = dto.Observaciones,
            Estado        = "Pagada"
        };

        _db.Facturas.Add(factura);
        await _db.SaveChangesAsync();

        // ── Correo al paciente ─────────────────────────────────────
        _ = Task.Run(async () =>
        {
            try
            {
                var pac = await _db.Pacientes
                    .Include(p => p.Usuario)
                    .FirstOrDefaultAsync(p => p.PacienteId == pacienteId);
                var email   = pac?.Usuario?.Email ?? "";
                var nombre  = $"{pac?.Usuario?.Nombre} {pac?.Usuario?.Apellido}".Trim();
                if (string.IsNullOrEmpty(email)) return;

                await _email.SendFacturaEmitidaAsync(
                    email, nombre, numero,
                    factura.Subtotal, factura.Impuesto, factura.Total, concepto);
            }
            catch { }
        });

        return Ok(factura);
    }

    [HttpPut("{id}/anular")]
    [Authorize(Roles = AuthRoles.SoloAdmin)]
    public async Task<IActionResult> Anular(int id)
    {
        var f = await _db.Facturas.FindAsync(id);
        if (f == null) return NotFound();
        f.Estado = "Anulada";
        await _db.SaveChangesAsync();
        return Ok(f);
    }

    [HttpGet("resumen")]
    [Authorize(Roles = AuthRoles.SoloAdmin)]
    public async Task<IActionResult> Resumen()
    {
        var hoy = DateTime.Today;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var facturas = await _db.Facturas.ToListAsync();
        return Ok(new
        {
            totalEmitidas  = facturas.Count,
            totalPagadas   = facturas.Count(f => f.Estado == "Pagada"),
            ingresosMes    = facturas.Where(f => f.FechaEmision >= inicioMes && f.Estado == "Pagada").Sum(f => f.Total),
            ingresosHoy    = facturas.Where(f => f.FechaEmision.Date == hoy && f.Estado == "Pagada").Sum(f => f.Total),
            pendientesPago = facturas.Count(f => f.Estado == "Emitida")
        });
    }
}

public class CrearFacturaDto
{
    public int PagoId { get; set; }
    public int? CitaId { get; set; }
    public int PacienteId { get; set; }
    public string? Concepto { get; set; }
    public string? Observaciones { get; set; }
}