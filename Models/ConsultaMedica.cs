using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // <--- Necesitas este para [Table]

namespace ClinicaAPI.Models;

[Table("ConsultasMedicas")] // <--- Esto le dice a C# que la tabla en SQL se llama así
public class ConsultaMedica
{
    [Key] // <--- Esto le dice que esta es la llave primaria aunque no se llame 'Id'
    public int ConsultaId { get; set; }

    public int CitaId { get; set; }
    public int DoctorId { get; set; }
    public int PacienteId { get; set; }
    public DateTime FechaConsulta { get; set; } = DateTime.Now;
    public string? Sintomas { get; set; }
    public string? Diagnostico { get; set; }
    public string? Tratamiento { get; set; }
    public string? Observaciones { get; set; }
    public string? PresionArterial { get; set; }
    public decimal? Temperatura { get; set; }
    public decimal? Peso { get; set; }
    public decimal? Altura { get; set; }
    public int? FrecuenciaCardiaca { get; set; }
    public decimal? SaturacionOxigeno { get; set; }
    public bool RequiereHospitalizacion { get; set; } = false;
    public DateOnly? ProximaCita { get; set; }

    // Relaciones (Asegúrate de que estos nombres coincidan con tus otras clases)
    public Cita? Cita { get; set; }
    public Doctor? Doctor { get; set; }
    public Paciente? Paciente { get; set; }
}