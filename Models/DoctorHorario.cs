using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClinicaAPI.Models;

public class DoctorHorario
{
    [Key] public int HorarioId { get; set; }
    public int DoctorId { get; set; }

    /// <summary>0=Dom 1=Lun 2=Mar 3=Mie 4=Jue 5=Vie 6=Sab</summary>
    public int DiaSemana { get; set; }

    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin    { get; set; }

    /// <summary>Duración de cada slot en minutos (30 ó 60).</summary>
    public int DuracionSlotMin { get; set; } = 30;

    public bool Activo { get; set; } = true;

    [JsonIgnore]
    public Doctor? Doctor { get; set; }
}