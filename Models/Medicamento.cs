using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; //Para arreglar el error
namespace ClinicaAPI.Models;

public class Medicamento
{
    [Key] //para arreglar el error
    public int MedicamentoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Presentacion { get; set; }
    public int Stock { get; set; } = 0;
    public decimal PrecioUnitario { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    public string? Proveedor { get; set; }
}