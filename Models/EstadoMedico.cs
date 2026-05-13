using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; //Para arreglar el error
namespace ClinicaAPI.Models;



public class EstadoMedico
{
    [Key] //para arreglar el error
    public int EstadoMedicoId { get; set; }
    public int DoctorId { get; set; }
    public string Estado { get; set; } = "Disponible";
    public int? CitaActualId { get; set; }
    public DateTime FechaHoraActualizacion { get; set; } = DateTime.Now;
    public string? Observacion { get; set; }

    public Doctor? Doctor { get; set; }
    public Cita? CitaActual { get; set; }
}