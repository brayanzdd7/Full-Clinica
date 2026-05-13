using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace ClinicaAPI.Models;

public class Factura
{
    [Key]
    public int FacturaId { get; set; }

    public string NumeroFactura { get; set; } = string.Empty;

    /// <summary>
    /// null cuando la factura es por una venta directa de medicamentos (sin cita).
    /// </summary>
    public int? CitaId { get; set; }

    public int PacienteId { get; set; }
    public int? PagoId { get; set; }

    public DateTime FechaEmision { get; set; } = DateTime.Now;
    public string Concepto { get; set; } = "Consulta médica";

    public decimal Subtotal  { get; set; }
    public decimal Impuesto  { get; set; } = 0;
    public decimal Total     { get; set; }

    public string Estado { get; set; } = "Emitida"; // Emitida | Pagada | Anulada
    public string? Observaciones { get; set; }

    [JsonIgnore]
    public Cita? Cita { get; set; }

    public Paciente? Paciente { get; set; }
    public Pago? Pago { get; set; }
}