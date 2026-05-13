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
[Authorize(Roles = AuthRoles.AdminYRecepcion)]
public class VentasController : ControllerBase
{
    private const decimal IVA = 0.12m;

    private readonly ClinicaContext _db;
    private readonly IAccessControlService _access;

    public VentasController(ClinicaContext db, IAccessControlService access)
    {
        _db = db;
        _access = access;
    }

    // GET /api/ventas
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.VentasDirectas
            .Include(v => v.Paciente).ThenInclude(p => p!.Usuario)
            .Include(v => v.MetodoPago)
            .Include(v => v.Detalles).ThenInclude(d => d.Medicamento)
            .OrderByDescending(v => v.FechaVenta)
            .ToListAsync());

    // GET /api/ventas/paciente/{pacienteId}
    // FIX: eliminado [AllowAnonymous] que conflictuaba con [Authorize] del controller.
    // El acceso se controla manualmente con PuedeVerPacienteAsync.
    [HttpGet("paciente/{pacienteId}")]
    public async Task<IActionResult> GetByPaciente(int pacienteId)
    {
        if (!await _access.PuedeVerPacienteAsync(pacienteId))
            return Forbid();

        return Ok(await _db.VentasDirectas
            .Include(v => v.MetodoPago)
            .Include(v => v.Detalles).ThenInclude(d => d.Medicamento)
            .Where(v => v.PacienteId == pacienteId)
            .OrderByDescending(v => v.FechaVenta)
            .ToListAsync());
    }

    // api/ventas
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CrearVentaDto dto)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            return BadRequest("Debes incluir al menos un medicamento.");

        decimal subtotal = 0;
        var detalles = new List<DetalleVentaDirecta>();

        foreach (var item in dto.Items)
        {
            var med = await _db.Medicamentos.FindAsync(item.MedicamentoId);
            if (med == null)
                return NotFound($"Medicamento {item.MedicamentoId} no encontrado.");

            if (med.Stock < item.Cantidad)
                return BadRequest($"Stock insuficiente de '{med.Nombre}' (disponible: {med.Stock}).");

            var linea = item.Cantidad * med.PrecioUnitario;
            subtotal += linea;

            detalles.Add(new DetalleVentaDirecta
            {
                MedicamentoId  = item.MedicamentoId,
                Cantidad       = item.Cantidad,
                PrecioUnitario = med.PrecioUnitario,
                Subtotal       = linea
            });
        }

        var impuesto = Math.Round(subtotal * IVA, 2);
        var total    = subtotal + impuesto;

        var venta = new VentaDirecta
        {
            PacienteId    = dto.PacienteId,
            MetodoPagoId  = dto.MetodoPagoId,
            FechaVenta    = DateTime.Now,
            Subtotal      = subtotal,
            Impuesto      = impuesto,
            Total         = total,
            Observaciones = dto.Observaciones,
            Estado        = "Completada",
            Detalles      = detalles
        };

        _db.VentasDirectas.Add(venta);
        await _db.SaveChangesAsync();

        // Descontar stock
        foreach (var det in venta.Detalles)
        {
            var med = await _db.Medicamentos.FindAsync(det.MedicamentoId);
            if (med != null) med.Stock -= det.Cantidad;
        }

        // Crear pago asociado
        var pago = new Pago
        {
            VentaDirectaId = venta.VentaId,
            MetodoPagoId   = dto.MetodoPagoId,
            Monto          = subtotal,
            Impuesto       = impuesto,
            Total          = total,
            FechaPago      = DateTime.Now,
            Estado         = "Completado"
        };
        _db.Pagos.Add(pago);

        // Registrar en historia clínica
        var historia = await _db.HistoriasClinicas
            .FirstOrDefaultAsync(h => h.PacienteId == dto.PacienteId);
        if (historia == null)
        {
            historia = new HistoriaClinica { PacienteId = dto.PacienteId };
            _db.HistoriasClinicas.Add(historia);
            await _db.SaveChangesAsync();
        }

        var medsDesc = string.Join(", ", detalles.Select(d => $"{d.Cantidad}x med#{d.MedicamentoId}"));

        _db.DetalleHistoriaClinica.Add(new DetalleHistoriaClinica
        {
            HistoriaId    = historia.HistoriaId,
            TipoEvento    = "Compra de medicamentos",
            Descripcion   = $"Venta directa: {medsDesc}. Total: Q{total:F2}",
            FechaRegistro = DateTime.Now
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            venta.VentaId,
            venta.Subtotal,
            venta.Impuesto,
            venta.Total,
            pagoId = pago.PagoId
        });
    }
}

public class CrearVentaDto
{
    public int PacienteId   { get; set; }
    public int MetodoPagoId { get; set; }
    public string? Observaciones { get; set; }
    public List<ItemVentaDto> Items { get; set; } = new();
}

public class ItemVentaDto
{
    public int MedicamentoId { get; set; }
    public int Cantidad      { get; set; }
}