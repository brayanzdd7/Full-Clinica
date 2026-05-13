using ClinicaAPI.Constants;
using ClinicaAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicaAPI.Data;

public static class RoleSeeder
{
    /// <summary>
    /// Asegura los 4 roles con nombres canónicos y migra el nombre legacy "Administrador" → "Admin".
    /// </summary>
    public static async Task SeedAsync(ClinicaContext db, CancellationToken cancellationToken = default)
    {
        var legacy = await db.Roles.FirstOrDefaultAsync(r => r.Nombre == "Administrador", cancellationToken);
        if (legacy != null)
        {
            legacy.Nombre = RoleNames.Admin;
            await db.SaveChangesAsync(cancellationToken);
        }

        foreach (var nombre in new[] { RoleNames.Admin, RoleNames.Doctor, RoleNames.Recepcionista, RoleNames.Paciente })
        {
            var existe = await db.Roles.AnyAsync(r => r.Nombre == nombre, cancellationToken);
            if (!existe)
                db.Roles.Add(new Rol { Nombre = nombre });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
