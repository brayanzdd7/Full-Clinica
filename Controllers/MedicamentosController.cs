using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClinicaAPI.Data;
using ClinicaAPI.Models;

namespace ClinicaAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MedicamentosController : ControllerBase
{
    private readonly ClinicaContext _context;
    public MedicamentosController(ClinicaContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _context.Medicamentos.ToListAsync());

    [HttpGet("stock-bajo")]
    public async Task<IActionResult> GetStockBajo() =>
        Ok(await _context.Medicamentos.Where(m => m.Stock < 10).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Medicamento med)
    {
        _context.Medicamentos.Add(med);
        await _context.SaveChangesAsync();
        return Ok(med);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Medicamento dto)
    {
        var med = await _context.Medicamentos.FindAsync(id);
        if (med == null) return NotFound();
        med.Nombre = dto.Nombre;
        med.Stock = dto.Stock;
        med.PrecioUnitario = dto.PrecioUnitario;
        med.FechaVencimiento = dto.FechaVencimiento;
        await _context.SaveChangesAsync();
        return Ok(med);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var med = await _context.Medicamentos.FindAsync(id);
        if (med == null) return NotFound();
        _context.Medicamentos.Remove(med);
        await _context.SaveChangesAsync();
        return Ok();
    }
}