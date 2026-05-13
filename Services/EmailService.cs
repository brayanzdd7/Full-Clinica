using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

namespace ClinicaAPI.Services;

public class SmtpConfig
{
    public string Host      { get; set; } = "smtp.gmail.com";
    public int    Port      { get; set; } = 587;
    public string User      { get; set; } = "";
    public string Password  { get; set; } = "";
    public string FromName  { get; set; } = "Clínica Médica";
    public string FromEmail { get; set; } = "";
}

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody);
    Task SendBienvenidaAsync(string email, string nombre, string apellido, string password, string rol);
    Task SendCitaAgendadaAsync(string emailPaciente, string emailDoctor,
        string nombrePaciente, string nombreDoctor, DateTime fechaHora, string? motivo);
    Task SendCitaCambioEstadoAsync(string emailPaciente, string emailDoctor,
        string nombrePaciente, string nombreDoctor, DateTime fechaHora, string estado);
    Task SendPagoRegistradoAsync(string emailPaciente, string nombrePaciente,
        decimal monto, decimal impuesto, decimal total, string metodoPago, string concepto);
    Task SendFacturaEmitidaAsync(string emailPaciente, string nombrePaciente,
        string numeroFactura, decimal subtotal, decimal impuesto, decimal total, string concepto);
}

public class EmailService : IEmailService
{
    private readonly SmtpConfig _cfg;
    private readonly ILogger<EmailService> _log;

    public EmailService(IOptions<SmtpConfig> cfg, ILogger<EmailService> log)
    {
        _cfg = cfg.Value;
        _log = log;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_cfg.User) || string.IsNullOrWhiteSpace(_cfg.Password))
        {
            _log.LogWarning("SMTP no configurado. Correo a {To} omitido.", to);
            return;
        }
        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_cfg.FromName,
                string.IsNullOrEmpty(_cfg.FromEmail) ? _cfg.User : _cfg.FromEmail));
            msg.To.Add(MailboxAddress.Parse(to));
            msg.Subject = subject;
            msg.Body    = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();

            var modo = _cfg.Port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(_cfg.Host, _cfg.Port, modo);
            await client.AuthenticateAsync(_cfg.User, _cfg.Password);
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);

            _log.LogInformation("Correo enviado a {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error al enviar correo a {To}: {Subject}", to, subject);
        }
    }

    private static string Layout(string titulo, string cuerpo) => $@"<!DOCTYPE html>
<html lang='es'><head><meta charset='UTF-8'><style>
body{{font-family:Arial,sans-serif;background:#f0f4f4;margin:0;padding:20px;}}
.card{{background:white;max-width:560px;margin:0 auto;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.08);}}
.hdr{{background:linear-gradient(135deg,#0fa8a0,#0a7a75);padding:28px 32px;color:white;}}
.hdr h1{{margin:0;font-size:1.2rem;font-weight:700;}}.hdr p{{margin:6px 0 0;font-size:.83rem;opacity:.85;}}
.body{{padding:28px 32px;}}.body h2{{font-size:1rem;color:#0a7a75;margin:0 0 14px;}}
.row{{display:flex;justify-content:space-between;padding:7px 0;border-bottom:1px solid #f0f0f0;font-size:.87rem;}}
.row:last-child{{border:none;}}.lbl{{color:#666;}}.val{{font-weight:600;}}.val.green{{color:#0a7a75;}}
.box{{background:#f0fafa;border-left:3px solid #0fa8a0;padding:11px 15px;border-radius:0 8px 8px 0;margin:14px 0;font-size:.86rem;}}
.cred{{background:#f8f8f8;border:1px solid #e0e0e0;border-radius:8px;padding:14px;margin:12px 0;}}
.cred .k{{color:#666;font-size:.72rem;text-transform:uppercase;letter-spacing:.5px;}}
.cred .v{{font-size:1rem;font-weight:700;color:#0a7a75;margin-top:3px;font-family:monospace;}}
.badge{{display:inline-block;padding:3px 10px;border-radius:20px;font-size:.76rem;font-weight:600;}}
.g{{background:#d1fae5;color:#065f46;}}.r{{background:#fee2e2;color:#991b1b;}}
.b{{background:#dbeafe;color:#1d4ed8;}}.y{{background:#fef3c7;color:#92400e;}}
.ftr{{background:#f8f8f8;padding:14px 32px;text-align:center;font-size:.73rem;color:#999;border-top:1px solid #eee;}}
</style></head><body>
<div class='card'>
  <div class='hdr'><h1>🏥 Clínica Médica</h1><p>{titulo}</p></div>
  <div class='body'>{cuerpo}</div>
  <div class='ftr'>Correo automático del Sistema de Gestión Clínica. No respondas a este mensaje.</div>
</div></body></html>";

    public async Task SendBienvenidaAsync(string email, string nombre, string apellido, string password, string rol)
    {
        var c = $@"<h2>¡Bienvenido/a, {nombre}!</h2>
<p style='font-size:.88rem;color:#555;margin-bottom:14px'>Tu cuenta fue creada en el Sistema de Gestión Clínica Médica.</p>
<div class='cred'><div class='k'>Correo</div><div class='v'>{email}</div></div>
<div class='cred'><div class='k'>Contraseña</div><div class='v'>{password}</div></div>
<div class='box'>Rol: <strong>{rol}</strong>. Cambia tu contraseña al iniciar sesión por primera vez.</div>";
        await SendAsync(email, "🏥 Bienvenido/a — Tus credenciales de acceso", Layout("Registro de nuevo usuario", c));
    }

    public async Task SendCitaAgendadaAsync(string emailPaciente, string emailDoctor,
        string nombrePaciente, string nombreDoctor, DateTime fechaHora, string? motivo)
    {
        var fecha = fechaHora.ToString("dd/MM/yyyy");
        var hora  = fechaHora.ToString("hh:mm tt");
        var mot   = motivo != null ? $"<div class='row'><span class='lbl'>Motivo</span><span class='val'>{motivo}</span></div>" : "";

        var cPac = $@"<h2>Cita médica confirmada</h2>
<div class='row'><span class='lbl'>Médico</span><span class='val'>{nombreDoctor}</span></div>
<div class='row'><span class='lbl'>Fecha</span><span class='val green'>{fecha}</span></div>
<div class='row'><span class='lbl'>Hora</span><span class='val green'>{hora}</span></div>{mot}
<div class='box'>Llega 10 minutos antes. Comunícate con recepción si necesitas cancelar.</div>";
        await SendAsync(emailPaciente, $"📅 Cita con {nombreDoctor} — {fecha} {hora}", Layout("Confirmación de cita", cPac));

        var cDoc = $@"<h2>Nueva cita en tu agenda</h2>
<div class='row'><span class='lbl'>Paciente</span><span class='val'>{nombrePaciente}</span></div>
<div class='row'><span class='lbl'>Fecha</span><span class='val green'>{fecha}</span></div>
<div class='row'><span class='lbl'>Hora</span><span class='val green'>{hora}</span></div>{mot}";
        await SendAsync(emailDoctor, $"📅 Nueva cita — {nombrePaciente} {fecha} {hora}", Layout("Nueva cita agendada", cDoc));
    }

    public async Task SendCitaCambioEstadoAsync(string emailPaciente, string emailDoctor,
        string nombrePaciente, string nombreDoctor, DateTime fechaHora, string estado)
    {
        var (cls, emoji) = estado switch {
            "Confirmada" => ("g","✅"), "Cancelada" => ("r","❌"),
            "Completada" => ("b","🎉"), _ => ("y","🔔") };
        var c = $@"<h2>Actualización de cita</h2>
<div class='row'><span class='lbl'>Estado</span><span class='val'><span class='badge {cls}'>{emoji} {estado}</span></span></div>
<div class='row'><span class='lbl'>Paciente</span><span class='val'>{nombrePaciente}</span></div>
<div class='row'><span class='lbl'>Médico</span><span class='val'>{nombreDoctor}</span></div>
<div class='row'><span class='lbl'>Fecha</span><span class='val'>{fechaHora:dd/MM/yyyy HH:mm}</span></div>";
        await SendAsync(emailPaciente, $"{emoji} Cita {fechaHora:dd/MM/yyyy}: {estado}", Layout("Actualización de cita", c));
        if (!string.IsNullOrEmpty(emailDoctor) && emailDoctor != emailPaciente)
            await SendAsync(emailDoctor, $"{emoji} Cita de {nombrePaciente} {fechaHora:dd/MM/yyyy}: {estado}", Layout("Actualización de cita", c));
    }

    public async Task SendPagoRegistradoAsync(string emailPaciente, string nombrePaciente,
        decimal monto, decimal impuesto, decimal total, string metodoPago, string concepto)
    {
        var c = $@"<h2>Pago registrado</h2>
<p style='font-size:.88rem;color:#555;margin-bottom:14px'>Hola <strong>{nombrePaciente}</strong>, tu pago fue registrado.</p>
<div class='row'><span class='lbl'>Concepto</span><span class='val'>{concepto}</span></div>
<div class='row'><span class='lbl'>Método</span><span class='val'>{metodoPago}</span></div>
<div class='row'><span class='lbl'>Subtotal</span><span class='val'>Q {monto:F2}</span></div>
<div class='row'><span class='lbl'>IVA (12%)</span><span class='val'>Q {impuesto:F2}</span></div>
<div class='row'><span class='lbl'>Total</span><span class='val green'>Q {total:F2}</span></div>
<div class='box'>Solicita tu factura en recepción si la necesitas.</div>";
        await SendAsync(emailPaciente, $"💳 Pago Q{total:F2} — {concepto}", Layout("Confirmación de pago", c));
    }

    public async Task SendFacturaEmitidaAsync(string emailPaciente, string nombrePaciente,
        string numeroFactura, decimal subtotal, decimal impuesto, decimal total, string concepto)
    {
        var c = $@"<h2>Factura emitida</h2>
<p style='font-size:.88rem;color:#555;margin-bottom:14px'>Estimado/a <strong>{nombrePaciente}</strong>, tu factura fue emitida.</p>
<div class='row'><span class='lbl'>N° Factura</span><span class='val green'>{numeroFactura}</span></div>
<div class='row'><span class='lbl'>Concepto</span><span class='val'>{concepto}</span></div>
<div class='row'><span class='lbl'>Subtotal</span><span class='val'>Q {subtotal:F2}</span></div>
<div class='row'><span class='lbl'>IVA (12%)</span><span class='val'>Q {impuesto:F2}</span></div>
<div class='row'><span class='lbl'>Total</span><span class='val green'>Q {total:F2}</span></div>
<div class='box'>Solicita una copia impresa en recepción.</div>";
        await SendAsync(emailPaciente, $"🧾 Factura {numeroFactura} — Q{total:F2}", Layout("Factura emitida", c));
    }
}