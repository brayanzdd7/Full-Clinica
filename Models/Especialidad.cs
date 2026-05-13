using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; //Para arreglar el error
namespace ClinicaAPI.Models;

public class Especialidad

{
    [Key] //para arreglar el error
    public int EspecialidadId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public ICollection<Doctor> Doctores { get; set; } = new List<Doctor>();
}