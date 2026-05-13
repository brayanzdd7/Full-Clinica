// ─── Cita.cs (ACTUALIZADO) ────────────────────────────────────────────────
// Se agregan FechaFin y DuracionMinutos para soporte de slots.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
namespace ClinicaAPI.Models;

public class Cita
{
    [Key] public int CitaId { get; set; }
    public int PacienteId { get; set; }
    public int DoctorId   { get; set; }

    public DateTime FechaHora { get; set; }

    /// <summary>
    /// Calculado automáticamente en el backend según el horario del doctor.
    /// FechaFin = FechaHora + DuracionMinutos
    /// </summary>
    public DateTime FechaFin { get; set; }

    /// <summary>Duración real de este slot (tomada del DoctorHorario).</summary>
    public int DuracionMinutos { get; set; } = 30;

    public string? Motivo   { get; set; }
    public string  Estado   { get; set; } = "Pendiente";
    public string? Notas    { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    public Doctor?  Doctor  { get; set; }
    public Paciente? Paciente { get; set; }

    [JsonIgnore] public Pago? Pago { get; set; }
    [JsonIgnore] public ConsultaMedica? ConsultaMedica { get; set; }
}