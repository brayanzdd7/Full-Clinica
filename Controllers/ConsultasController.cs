using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClinicaAPI.Data;
using ClinicaAPI.Models;

namespace ClinicaAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConsultasController : ControllerBase
{
    private readonly ClinicaContext _context;
    public ConsultasController(ClinicaContext context) => _context = context;

    [HttpGet("paciente/{pacienteId}")]
    public async Task<IActionResult> GetByPaciente(int pacienteId) =>
        Ok(await _context.ConsultasMedicas
            .Include(c => c.Doctor).ThenInclude(d => d!.Usuario)
            .Where(c => c.PacienteId == pacienteId)
            .OrderByDescending(c => c.FechaConsulta)
            .ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ConsultaMedica consulta)
    {
        consulta.FechaConsulta = DateTime.Now;
        _context.ConsultasMedicas.Add(consulta);
        await _context.SaveChangesAsync();

        // Agregar a historia clínica automáticamente
        var historia = await _context.HistoriasClinicas
            .FirstOrDefaultAsync(h => h.PacienteId == consulta.PacienteId);

        if (historia == null)
        {
            historia = new HistoriaClinica { PacienteId = consulta.PacienteId };
            _context.HistoriasClinicas.Add(historia);
            await _context.SaveChangesAsync();
        }

        _context.DetalleHistoriaClinica.Add(new DetalleHistoriaClinica
        {
            HistoriaId = historia.HistoriaId,
            ConsultaId = consulta.ConsultaId,
            TipoEvento = "Consulta",
            Descripcion = consulta.Diagnostico,
            FechaRegistro = DateTime.Now
        });

        // Cambiar estado de la cita a Completada
        var cita = await _context.Citas.FindAsync(consulta.CitaId);
        if (cita != null) cita.Estado = "Completada";

        await _context.SaveChangesAsync();
        return Ok(consulta);
    }
}