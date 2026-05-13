using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClinicaAPI.Data;
using ClinicaAPI.Models;
using ClinicaAPI.Services;

namespace ClinicaAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PacientesController : ControllerBase
{
    private readonly ClinicaContext _db;
    private readonly IAccessControlService _access;

    public PacientesController(ClinicaContext db, IAccessControlService access)
    {
        _db     = db;
        _access = access;
    }

[HttpGet]
public async Task<IActionResult> GetAll([FromQuery] bool incluirInactivos = false)
{
    var query = _db.Pacientes.Include(p => p.Usuario).AsQueryable();
    if (!incluirInactivos)
        query = query.Where(p => p.Usuario!.Activo);
    return Ok(await query.ToListAsync());
}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!await _access.PuedeVerPacienteAsync(id))
            return Forbid();

        var paciente = await _db.Pacientes
            .Include(p => p.Usuario)
            .Include(p => p.HistoriaClinica)
            .FirstOrDefaultAsync(p => p.PacienteId == id);
        return paciente == null ? NotFound() : Ok(paciente);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Paciente dto)
    {
        if (!await _access.PuedeVerPacienteAsync(id))
            return Forbid();

        var paciente = await _db.Pacientes.FindAsync(id);
        if (paciente == null) return NotFound();
        paciente.FechaNacimiento = dto.FechaNacimiento;
        paciente.Genero          = dto.Genero;
        paciente.Telefono        = dto.Telefono;
        paciente.Direccion       = dto.Direccion;
        paciente.TipoSangre      = dto.TipoSangre;
        paciente.Alergias        = dto.Alergias;
        await _db.SaveChangesAsync();
        return Ok(paciente);
    }

    // ── GET /api/pacientes/{id}/historial ──────────────────────────────────────
    [HttpGet("{id}/historial")]
    public async Task<IActionResult> GetHistorial(int id)
    {
        if (!await _access.PuedeVerPacienteAsync(id))
            return Forbid();

        // ── Citas ────────────────────────────────────────────────
        var citas = await _db.Citas
            .AsNoTracking()
            .Include(c => c.Doctor).ThenInclude(d => d!.Usuario)
            .Include(c => c.Doctor).ThenInclude(d => d!.Especialidad)
            .Where(c => c.PacienteId == id)
            .OrderByDescending(c => c.FechaHora)
            .Select(c => new
            {
                tipo   = "cita",
                fecha  = c.FechaHora,
                c.CitaId,
                c.Estado,
                c.Motivo,
                doctor = c.Doctor == null ? null : new
                {
                    nombre       = c.Doctor.Usuario!.Nombre,
                    apellido     = c.Doctor.Usuario.Apellido,
                    especialidad = c.Doctor.Especialidad!.Nombre
                }
            })
            .ToListAsync();

        // ── Recetas ──────────────────────────────────────────────
        // FIX: d.Medicamento puede ser null cuando la línea es texto libre (NombreLibre).
        // Usar el operador condicional para evitar NullReferenceException.
        var recetas = await _db.Recetas
            .AsNoTracking()
            .Include(r => r.Doctor).ThenInclude(d => d!.Usuario)
            .Include(r => r.Detalles).ThenInclude(d => d.Medicamento)
            .Where(r => r.PacienteId == id)
            .OrderByDescending(r => r.FechaEmision)
            .Select(r => new
            {
                tipo   = "receta",
                fecha  = r.FechaEmision,
                r.RecetaId,
                r.Observaciones,
                doctor = r.Doctor == null ? null : new
                {
                    nombre   = r.Doctor.Usuario!.Nombre,
                    apellido = r.Doctor.Usuario.Apellido
                },
                // FIX: Medicamento puede ser null → usar NombreLibre como fallback
                medicamentos = r.Detalles.Select(d => new
                {
                    nombre      = d.Medicamento != null ? d.Medicamento.Nombre : d.NombreLibre,
                    nombreLibre = d.NombreLibre,
                    d.Dosis,
                    d.Frecuencia,
                    d.Duracion,
                    d.Cantidad
                })
            })
            .ToListAsync();

        // ── Pagos de citas ────────────────────────────────────────
        var citaIds = await _db.Citas.AsNoTracking()
            .Where(c => c.PacienteId == id)
            .Select(c => c.CitaId)
            .ToListAsync();

        var pagosCita = citaIds.Count > 0
            ? await _db.Pagos.AsNoTracking()
                .Include(p => p.MetodoPago)
                .Where(p => p.CitaId.HasValue && citaIds.Contains(p.CitaId.Value))
                .OrderByDescending(p => p.FechaPago)
                .Select(p => new PagoHistorialItem
                {
                    Tipo     = "pago",
                    Fecha    = p.FechaPago,
                    PagoId   = p.PagoId,
                    Monto    = p.Monto,
                    Impuesto = p.Impuesto,
                    Total    = p.Total,
                    Estado   = p.Estado,
                    Metodo   = p.MetodoPago!.Nombre,
                    Concepto = "Consulta médica"
                })
                .ToListAsync()
            : new List<PagoHistorialItem>();

        // ── Pagos de ventas directas ──────────────────────────────
        var ventaIds = await _db.VentasDirectas.AsNoTracking()
            .Where(v => v.PacienteId == id)
            .Select(v => v.VentaId)
            .ToListAsync();

        if (ventaIds.Count > 0)
        {
            var pagosVenta = await _db.Pagos.AsNoTracking()
                .Include(p => p.MetodoPago)
                .Where(p => p.VentaDirectaId.HasValue && ventaIds.Contains(p.VentaDirectaId.Value))
                .OrderByDescending(p => p.FechaPago)
                .Select(p => new PagoHistorialItem
                {
                    Tipo     = "pago",
                    Fecha    = p.FechaPago,
                    PagoId   = p.PagoId,
                    Monto    = p.Monto,
                    Impuesto = p.Impuesto,
                    Total    = p.Total,
                    Estado   = p.Estado,
                    Metodo   = p.MetodoPago!.Nombre,
                    Concepto = "Compra de medicamentos"
                })
                .ToListAsync();
            pagosCita.AddRange(pagosVenta);
        }

        var pagos = pagosCita.OrderByDescending(p => p.Fecha).ToList();

        // ── Facturas ──────────────────────────────────────────────
        var facturas = await _db.Facturas.AsNoTracking()
            .Include(f => f.Pago).ThenInclude(p => p!.MetodoPago)
            .Where(f => f.PacienteId == id)
            .OrderByDescending(f => f.FechaEmision)
            .Select(f => new
            {
                tipo            = "factura",
                fecha           = f.FechaEmision,
                f.FacturaId,
                f.NumeroFactura,
                f.Concepto,
                f.Subtotal,
                f.Impuesto,
                f.Total,
                f.Estado,
                metodo = f.Pago != null ? f.Pago.MetodoPago!.Nombre : null
            })
            .ToListAsync();

        var historia = await _db.HistoriasClinicas.AsNoTracking()
            .FirstOrDefaultAsync(h => h.PacienteId == id);

        return Ok(new
        {
            historiaClinica = historia,
            citas,
            recetas,
            pagos,
            facturas,
            resumen = new
            {
                totalCitas    = citas.Count,
                totalRecetas  = recetas.Count,
                totalPagado   = pagos.Sum(p => p.Total),
                totalFacturas = facturas.Count
            }
        });
    }
}

// DTO tipado para evitar el error con dynamic en el Sum del resumen
public class PagoHistorialItem
{
    public string   Tipo     { get; set; } = "pago";
    public DateTime Fecha    { get; set; }
    public int      PagoId   { get; set; }
    public decimal  Monto    { get; set; }
    public decimal  Impuesto { get; set; }
    public decimal  Total    { get; set; }
    public string?  Estado   { get; set; }
    public string?  Metodo   { get; set; }
    public string?  Concepto { get; set; }
}