using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; //Para arreglar el error
namespace ClinicaAPI.Models;

public class Doctor
{

    [Key] //para arreglar el error    
    public int DoctorId { get; set; }
    public int UsuarioId { get; set; }
    public int EspecialidadId { get; set; }
    public string? NumeroLicencia { get; set; }
    public string? Telefono { get; set; }
    public string? FotoUrl { get; set; }

    public Usuario? Usuario { get; set; }
    public Especialidad? Especialidad { get; set; }
    public ICollection<Cita> Citas { get; set; } = new List<Cita>();
    public EstadoMedico? EstadoMedico { get; set; }
}