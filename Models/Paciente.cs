using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; //Para arreglar el error
namespace ClinicaAPI.Models;

public class Paciente
{
    [Key] //para arreglar el error
    public int PacienteId { get; set; }
    public int UsuarioId { get; set; }
    public DateOnly? FechaNacimiento { get; set; }
    public string? Genero { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public string? TipoSangre { get; set; }
    public string? Alergias { get; set; }

    public Usuario? Usuario { get; set; }
    public ICollection<Cita> Citas { get; set; } = new List<Cita>();
    public HistoriaClinica? HistoriaClinica { get; set; }
}