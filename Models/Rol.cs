using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; //Para arreglar el error
namespace ClinicaAPI.Models;

[Table("Roles")]
public class Rol

{
    [Key] //para arreglar el error
    public int RolId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    [InverseProperty("Rol")]
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}