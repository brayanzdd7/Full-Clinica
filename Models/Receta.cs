using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; //Para arreglar el error
namespace ClinicaAPI.Models;

public class Receta
{
    [Key] //para arreglar el error
    public int RecetaId { get; set; }
    public int CitaId { get; set; }
    public int DoctorId { get; set; }
    public int PacienteId { get; set; }
    public DateTime FechaEmision { get; set; } = DateTime.Now;
    public string? Observaciones { get; set; }

    public Cita? Cita { get; set; }
    public Doctor? Doctor { get; set; }
    public Paciente? Paciente { get; set; }
    public ICollection<DetalleReceta> Detalles { get; set; } = new List<DetalleReceta>();
}