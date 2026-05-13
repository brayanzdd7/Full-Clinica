using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace ClinicaAPI.Models;

public class HistoriaClinica
{
    [Key] //para arreglar el error
    public int HistoriaId { get; set; }
    public int PacienteId { get; set; }
    public DateTime FechaApertura { get; set; } = DateTime.Now;
    public string? EnfermedadesCronicas { get; set; }
    public string? AntecedentesFamiliares { get; set; }
    public string? AntecedentesQuirurgicos { get; set; }
    public string? AntecedentesPerinatales { get; set; }
    public string? Vacunas { get; set; }
    public string? Observaciones { get; set; }

    public Paciente? Paciente { get; set; }
    public ICollection<DetalleHistoriaClinica> Detalles { get; set; } = new List<DetalleHistoriaClinica>();
}