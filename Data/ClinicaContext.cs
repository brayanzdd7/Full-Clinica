using ClinicaAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicaAPI.Data;

public class ClinicaContext : DbContext
{
    public ClinicaContext(DbContextOptions<ClinicaContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── Rol ────────────────────────────────────────────────
        mb.Entity<Rol>(e =>
        {
            e.ToTable("Roles");
            e.HasIndex(r => r.Nombre).IsUnique();
        });

        // ── Usuario ────────────────────────────────────────────
        mb.Entity<Usuario>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasOne(u => u.Rol).WithMany(r => r.Usuarios)
                .HasForeignKey(u => u.RolId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(u => u.Doctor).WithOne(d => d.Usuario)
                .HasForeignKey<Doctor>(d => d.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(u => u.Paciente).WithOne(p => p.Usuario)
                .HasForeignKey<Paciente>(p => p.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Especialidad ───────────────────────────────────────
        mb.Entity<Especialidad>(e =>
        {
            e.HasMany(s => s.Doctores).WithOne(d => d.Especialidad)
                .HasForeignKey(d => d.EspecialidadId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── Doctor ─────────────────────────────────────────────
        mb.Entity<Doctor>(e =>
        {
            e.HasMany(d => d.Citas).WithOne(c => c.Doctor)
                .HasForeignKey(c => c.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.EstadoMedico).WithOne(em => em.Doctor)
                .HasForeignKey<EstadoMedico>(em => em.DoctorId).OnDelete(DeleteBehavior.Cascade);

            e.HasMany<DoctorHorario>().WithOne(h => h.Doctor)
                .HasForeignKey(h => h.DoctorId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany<BloqueoHorario>().WithOne(b => b.Doctor)
                .HasForeignKey(b => b.DoctorId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Paciente ───────────────────────────────────────────
        mb.Entity<Paciente>(e =>
        {
            e.HasMany(p => p.Citas).WithOne(c => c.Paciente)
                .HasForeignKey(c => c.PacienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.HistoriaClinica).WithOne(h => h.Paciente)
                .HasForeignKey<HistoriaClinica>(h => h.PacienteId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Cita ───────────────────────────────────────────────
        mb.Entity<Cita>(e =>
        {
            e.HasOne(c => c.Pago).WithOne(p => p.Cita)
                .HasForeignKey<Pago>(p => p.CitaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.ConsultaMedica).WithOne(cm => cm.Cita)
                .HasForeignKey<ConsultaMedica>(cm => cm.CitaId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── ConsultaMedica ─────────────────────────────────────
        mb.Entity<ConsultaMedica>(e =>
        {
            e.ToTable("ConsultasMedicas");
            e.Property(cm => cm.Temperatura).HasPrecision(5, 2);
            e.Property(cm => cm.Peso).HasPrecision(6, 3);
            e.Property(cm => cm.Altura).HasPrecision(5, 2);
            e.Property(cm => cm.SaturacionOxigeno).HasPrecision(5, 2);
            e.HasOne(cm => cm.Doctor).WithMany()
                .HasForeignKey(cm => cm.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(cm => cm.Paciente).WithMany()
                .HasForeignKey(cm => cm.PacienteId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── HistoriaClinica ────────────────────────────────────
        mb.Entity<HistoriaClinica>(e =>
        {
            e.HasMany(h => h.Detalles).WithOne(d => d.Historia)
                .HasForeignKey(d => d.HistoriaId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<DetalleHistoriaClinica>(e =>
        {
            e.HasOne(d => d.Consulta).WithMany()
                .HasForeignKey(d => d.ConsultaId).OnDelete(DeleteBehavior.SetNull);
        });

        // ── Receta ─────────────────────────────────────────────
        mb.Entity<Receta>(e =>
        {
            e.HasMany(r => r.Detalles).WithOne(d => d.Receta)
                .HasForeignKey(d => d.RecetaId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Cita).WithMany()
                .HasForeignKey(r => r.CitaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Doctor).WithMany()
                .HasForeignKey(r => r.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Paciente).WithMany()
                .HasForeignKey(r => r.PacienteId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── Cambio 7: MedicamentoId ahora es nullable ──────────
        mb.Entity<DetalleReceta>(e =>
        {
            e.HasOne(d => d.Medicamento).WithMany()
                .HasForeignKey(d => d.MedicamentoId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);   // ← permite NombreLibre sin medicamento
        });

        // ── Pago ───────────────────────────────────────────────
        mb.Entity<Pago>(e =>
        {
            e.Property(p => p.Monto).HasPrecision(18, 2);
            e.Property(p => p.Impuesto).HasPrecision(18, 2);
            e.Property(p => p.Total).HasPrecision(18, 2);
            e.HasOne(p => p.MetodoPago).WithMany()
                .HasForeignKey(p => p.MetodoPagoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.VentaDirecta).WithMany()
                .HasForeignKey(p => p.VentaDirectaId).OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });

        // ── Medicamento ────────────────────────────────────────
        mb.Entity<Medicamento>(e =>
        {
            e.Property(m => m.PrecioUnitario).HasPrecision(18, 2);
        });

        // ── Factura ────────────────────────────────────────────
        mb.Entity<Factura>(e =>
        {
            e.Property(f => f.Subtotal).HasPrecision(18, 2);
            e.Property(f => f.Impuesto).HasPrecision(18, 2);
            e.Property(f => f.Total).HasPrecision(18, 2);
            e.HasOne(f => f.Cita).WithMany()
                .HasForeignKey(f => f.CitaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Paciente).WithMany()
                .HasForeignKey(f => f.PacienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Pago).WithMany()
                .HasForeignKey(f => f.PagoId).OnDelete(DeleteBehavior.SetNull);
        });

        // ── EstadoMedico ───────────────────────────────────────
        mb.Entity<EstadoMedico>(e =>
        {
            e.HasOne(em => em.CitaActual).WithMany()
                .HasForeignKey(em => em.CitaActualId).OnDelete(DeleteBehavior.SetNull);
        });

        // ── VentaDirecta ───────────────────────────────────────
        mb.Entity<VentaDirecta>(e =>
        {
            e.Property(v => v.Subtotal).HasPrecision(18, 2);
            e.Property(v => v.Impuesto).HasPrecision(18, 2);
            e.Property(v => v.Total).HasPrecision(18, 2);
            e.HasOne(v => v.Paciente).WithMany()
                .HasForeignKey(v => v.PacienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(v => v.MetodoPago).WithMany()
                .HasForeignKey(v => v.MetodoPagoId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(v => v.Detalles).WithOne(d => d.Venta)
                .HasForeignKey(d => d.VentaId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<DetalleVentaDirecta>(e =>
        {
            e.Property(d => d.PrecioUnitario).HasPrecision(18, 2);
            e.Property(d => d.Subtotal).HasPrecision(18, 2);
            e.HasOne(d => d.Medicamento).WithMany()
                .HasForeignKey(d => d.MedicamentoId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    // ── DbSets ─────────────────────────────────────────────────
    public DbSet<Rol>                    Roles                   { get; set; }
    public DbSet<Usuario>                Usuarios                { get; set; }
    public DbSet<Especialidad>           Especialidades          { get; set; }
    public DbSet<Doctor>                 Doctores                { get; set; }
    public DbSet<Paciente>               Pacientes               { get; set; }
    public DbSet<Cita>                   Citas                   { get; set; }
    public DbSet<EstadoMedico>           EstadoMedico            { get; set; }
    public DbSet<Medicamento>            Medicamentos            { get; set; }
    public DbSet<Receta>                 Recetas                 { get; set; }
    public DbSet<DetalleReceta>          DetalleReceta           { get; set; }
    public DbSet<Pago>                   Pagos                   { get; set; }
    public DbSet<MetodoPago>             MetodosPago             { get; set; }
    public DbSet<ConsultaMedica>         ConsultasMedicas        { get; set; }
    public DbSet<HistoriaClinica>        HistoriasClinicas       { get; set; }
    public DbSet<DetalleHistoriaClinica> DetalleHistoriaClinica  { get; set; }
    public DbSet<Factura>                Facturas                { get; set; }
    public DbSet<DoctorHorario>          DoctorHorarios          { get; set; }
    public DbSet<BloqueoHorario>         BloqueosHorario         { get; set; }
    public DbSet<VentaDirecta>           VentasDirectas          { get; set; }
    public DbSet<DetalleVentaDirecta>    DetallesVentaDirecta    { get; set; }
}