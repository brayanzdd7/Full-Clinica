using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; //Para arreglar el error
namespace ClinicaAPI.Models;

public class MetodoPago
{
    [Key]
    public int MetodoPagoId { get; set; }

    public string Nombre { get; set; } = string.Empty;
}