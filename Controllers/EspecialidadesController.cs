using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClinicaAPI.Data;
using ClinicaAPI.Models;

namespace ClinicaAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EspecialidadesController : ControllerBase
{
    private readonly ClinicaContext _context;
    public EspecialidadesController(ClinicaContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _context.Especialidades.AsNoTracking().OrderBy(e => e.Nombre).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Especialidad esp)
    {
        _context.Especialidades.Add(esp);
        await _context.SaveChangesAsync();
        return Ok(esp);
    }
}