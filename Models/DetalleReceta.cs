using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClinicaAPI.Models;

public class DetalleReceta
{
    [Key]                          // ← FIX: PK correcta, no NombreLibre
    public int DetalleId { get; set; }

    public int RecetaId { get; set; }

    /// <summary>
    /// Nullable: null cuando la línea viene de texto libre (NombreLibre).
    /// </summary>
    public int? MedicamentoId { get; set; }

    /// <summary>
    /// Nombre libre del medicamento cuando no existe en el catálogo.
    /// Uno de los dos (MedicamentoId o NombreLibre) debe tener valor.
    /// </summary>
    public string? NombreLibre { get; set; }

    public string? Dosis      { get; set; }
    public string? Frecuencia { get; set; }
    public string? Duracion   { get; set; }
    public int     Cantidad   { get; set; } = 1;

    [JsonIgnore]
    public Receta?      Receta      { get; set; }
    public Medicamento? Medicamento { get; set; }
}