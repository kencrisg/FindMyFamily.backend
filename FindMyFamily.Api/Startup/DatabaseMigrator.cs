using FindMyFamily.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FindMyFamily.Api.Startup;

/// <summary>
/// Aplica las migraciones pendientes al arrancar.
///
/// Se activa con RUN_MIGRATIONS_ON_STARTUP=true. Existe porque en un alojamiento gratuito
/// no hay un sitio cómodo donde ejecutar `dotnet ef database update` contra la base remota.
/// En local conviene dejarlo apagado y migrar a mano, para no aplicar cambios de esquema
/// sin darse cuenta.
/// </summary>
public static class DatabaseMigrator
{
    public static async Task MigrateAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pendientes = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        if (pendientes.Count == 0)
        {
            logger.LogInformation("Base de datos al día: no hay migraciones pendientes.");
            return;
        }

        logger.LogInformation("Aplicando {Count} migraciones pendientes: {Migraciones}",
            pendientes.Count, string.Join(", ", pendientes));

        await context.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Migraciones aplicadas correctamente.");
    }
}
