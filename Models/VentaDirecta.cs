using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClinicaAPI.Models;

public class VentaDirecta
{
    [Key] public int VentaId { get; set; }
    public int PacienteId { get; set; }
    public int MetodoPagoId { get; set; }
    public DateTime FechaVenta { get; set; } = DateTime.Now;
    public decimal Subtotal { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }
    public string Estado { get; set; } = "Completada";

    public Paciente? Paciente { get; set; }
    public MetodoPago? MetodoPago { get; set; }
    public ICollection<DetalleVentaDirecta> Detalles { get; set; } = new List<DetalleVentaDirecta>();
}