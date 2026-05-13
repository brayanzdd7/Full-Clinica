using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClinicaAPI.Constants;
using ClinicaAPI.Data;
using ClinicaAPI.Models;
using ClinicaAPI.Services;

namespace ClinicaAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ClinicaContext _context;
    private readonly IConfiguration _config;
    private readonly IEmailService  _email;   // ← NUEVO

    public AuthController(ClinicaContext context, IConfiguration config, IEmailService email)
    {
        _context = context;
        _config  = config;
        _email   = email;   // ← NUEVO
    }

    // ── Registro genérico (paciente / admin / recepcionista) ───
    [HttpPost("registro")]
    public async Task<IActionResult> Registro([FromBody] RegistroDto dto)
    {
        if (await _context.Usuarios.AnyAsync(u => u.Email == dto.Email))
            return BadRequest("El email ya está registrado.");

        var passwordPlano = dto.Password;   // ← capturar ANTES de hashear

        var usuario = new Usuario
        {
            Nombre       = dto.Nombre,
            Apellido     = dto.Apellido,
            Email        = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            RolId        = dto.RolId
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        // Si es paciente, crear perfil automáticamente (RolId = 3)
        if (dto.RolId == 3)
        {
            _context.Pacientes.Add(new Paciente { UsuarioId = usuario.UsuarioId });
            await _context.SaveChangesAsync();
        }

     
        var rolNombre = dto.RolId switch { 1 => "Administrador", 2 => "Doctor", 3 => "Paciente", _ => "Recepcionista" };
        _ = Task.Run(() => _email.SendBienvenidaAsync(dto.Email, dto.Nombre, dto.Apellido, passwordPlano, rolNombre));

        return Ok(new { mensaje = "Usuario registrado correctamente", usuarioId = usuario.UsuarioId });
    }

    // ── Registro de Doctor (usuario + perfil doctor en 1 paso) ─
    [HttpPost("registro-doctor")]
    public async Task<IActionResult> RegistroDoctor([FromBody] RegistroDoctorDto dto)
    {
        if (await _context.Usuarios.AnyAsync(u => u.Email == dto.Email))
            return BadRequest("El email ya está registrado.");

        var passwordPlano = dto.Password;   // ← capturar ANTES de hashear

        var usuario = new Usuario
        {
            Nombre       = dto.Nombre,
            Apellido     = dto.Apellido,
            Email        = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            RolId        = 2
        };
        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        var doctor = new Doctor
        {
            UsuarioId      = usuario.UsuarioId,
            EspecialidadId = dto.EspecialidadId,
            NumeroLicencia = dto.NumeroLicencia,
            Telefono       = dto.Telefono
        };
        _context.Doctores.Add(doctor);
        await _context.SaveChangesAsync();

        _context.EstadoMedico.Add(new EstadoMedico
        {
            DoctorId = doctor.DoctorId,
            Estado   = "Disponible"
        });
        await _context.SaveChangesAsync();

        // ← NUEVO: correo de bienvenida al médico
        _ = Task.Run(() => _email.SendBienvenidaAsync(dto.Email, dto.Nombre, dto.Apellido, passwordPlano, "Doctor"));

        return Ok(new
        {
            mensaje   = "Médico registrado correctamente",
            usuarioId = usuario.UsuarioId,
            doctorId  = doctor.DoctorId
        });
    }

    // ── Login ──────────────────────────────────────────────────
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (usuario == null || !BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash))
            return Unauthorized("Credenciales incorrectas.");

        if (!usuario.Activo)
            return Unauthorized("Tu cuenta ha sido desactivada. Contacta al administrador.");

        int? doctorId   = null;
        int? pacienteId = null;

        if (usuario.Rol!.Nombre == "Doctor")
        {
            var doc = await _context.Doctores.FirstOrDefaultAsync(d => d.UsuarioId == usuario.UsuarioId);
            doctorId = doc?.DoctorId;
        }
        else if (usuario.Rol.Nombre == "Paciente")
        {
            var pac = await _context.Pacientes.FirstOrDefaultAsync(p => p.UsuarioId == usuario.UsuarioId);
            pacienteId = pac?.PacienteId;
        }

        var token = GenerarToken(usuario);
        return Ok(new
        {
            token,
            usuarioId  = usuario.UsuarioId,
            rol        = usuario.Rol!.Nombre,
            nombre     = usuario.Nombre,
            apellido   = usuario.Apellido,
            doctorId,
            pacienteId
        });
    }

    // ── Activar / Desactivar usuario ───────────────────────────
    [HttpPut("usuarios/{id}/activo")]
    [Authorize(Roles = AuthRoles.AdminYRecepcion)]
    public async Task<IActionResult> CambiarActivo(int id, [FromBody] CambiarActivoDto dto)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound("Usuario no encontrado.");
        usuario.Activo = dto.Activo;
        await _context.SaveChangesAsync();
        return Ok(new { usuario.UsuarioId, usuario.Activo });
    }

    private string GenerarToken(Usuario usuario)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.UsuarioId.ToString()),
            new Claim(ClaimTypes.Email,          usuario.Email),
            new Claim(ClaimTypes.Role,           usuario.Rol!.Nombre)
        };
        var token = new JwtSecurityToken(
            _config["Jwt:Issuer"],
            _config["Jwt:Audience"],
            claims,
            expires:           DateTime.Now.AddDays(7),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// ── DTOs ───────────────────────────────────────────────────────
public class LoginDto
{
    public string Email    { get; set; } = "";
    public string Password { get; set; } = "";
}

public class RegistroDto
{
    public string Nombre   { get; set; } = "";
    public string Apellido { get; set; } = "";
    public string Email    { get; set; } = "";
    public string Password { get; set; } = "";
    public int    RolId    { get; set; }
}

public class RegistroDoctorDto
{
    public string  Nombre          { get; set; } = "";
    public string  Apellido        { get; set; } = "";
    public string  Email           { get; set; } = "";
    public string  Password        { get; set; } = "";
    public int     EspecialidadId  { get; set; }
    public string? NumeroLicencia  { get; set; }
    public string? Telefono        { get; set; }
}

public class CambiarActivoDto
{
    public bool Activo { get; set; }
}