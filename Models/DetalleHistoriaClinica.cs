using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace ClinicaAPI.Models;

public class DetalleHistoriaClinica
{
    [Key] // <--- Esto soluciona el error de tu última i
    public int DetalleHistoriaId { get; set; }
    public int HistoriaId { get; set; }
    public int? ConsultaId { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
    public string? TipoEvento { get; set; }
    public string? Descripcion { get; set; }

    public HistoriaClinica? Historia { get; set; }
    public ConsultaMedica? Consulta { get; set; }
}