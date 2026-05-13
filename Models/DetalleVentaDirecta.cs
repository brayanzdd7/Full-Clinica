using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClinicaAPI.Models;

public class DetalleVentaDirecta
{
    [Key] public int DetalleVentaId { get; set; }
    public int VentaId { get; set; }
    public int MedicamentoId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }

    [JsonIgnore]
    public VentaDirecta? Venta { get; set; }
    public Medicamento? Medicamento { get; set; }
}