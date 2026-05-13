using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClinicaAPI.Models;

public class BloqueoHorario
{
    [Key] public int BloqueoId { get; set; }
    public int DoctorId { get; set; }

    public DateOnly Fecha { get; set; }

    /// <summary>null = bloqueo de día completo</summary>
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFin    { get; set; }
    public string? Motivo { get; set; }

    [JsonIgnore]
    public Doctor? Doctor { get; set; }
}