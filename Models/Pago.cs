// ─── Pago.cs (ACTUALIZADO) ────────────────────────────────────────────────
// CitaId es ahora nullable: puede ser pago de cita normal O venta directa.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
namespace ClinicaAPI.Models;

public class Pago
{
    [Key] public int PagoId { get; set; }

    /// <summary>null cuando el pago es por una VentaDirecta (sin cita).</summary>
    public int? CitaId { get; set; }

    /// <summary>null cuando el pago es por una Cita (sin venta directa).</summary>
    public int? VentaDirectaId { get; set; }

    public int MetodoPagoId { get; set; }
    public decimal Monto    { get; set; }
    public decimal Impuesto { get; set; } = 0;   // 12% calculado en backend
    public decimal Total    { get; set; }        // Monto + Impuesto
    public DateTime FechaPago { get; set; } = DateTime.Now;
    public string Estado { get; set; } = "Completado";

    [JsonIgnore] public Cita? Cita { get; set; }
    public MetodoPago? MetodoPago { get; set; }
    [JsonIgnore] public VentaDirecta? VentaDirecta { get; set; }
}