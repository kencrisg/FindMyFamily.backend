using FindMyFamily.Core.Common;
using FindMyFamily.Core.Entities;
using FindMyFamily.Core.Interfaces.Repositories;
using FindMyFamily.Core.Interfaces.Services;

namespace FindMyFamily.Api.Startup;

/// <summary>
/// Crea el Administrador de Sistema inicial a partir de variables de entorno.
///
/// Existe porque el sistema no tenía forma de arrancar: el único endpoint capaz de crear
/// usuarios exige ya ser superadmin, y ninguna migración sembraba uno. La única salida era
/// un INSERT manual con un hash BCrypt generado aparte.
///
/// Es idempotente y conservador: si ya existe cualquier usuario con rol 'admin', no hace
/// nada. Así no puede usarse para añadir superadmins una vez el sistema está en marcha.
/// </summary>
public static class SuperAdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var phone = configuration["SUPERADMIN_PHONE"];
        var password = configuration["SUPERADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation(
                "SUPERADMIN_PHONE/SUPERADMIN_PASSWORD no configurados: se omite la creación del Administrador de Sistema inicial.");
            return;
        }

        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        if (await userRepository.AnySystemAdminAsync(cancellationToken))
        {
            logger.LogInformation("Ya existe un Administrador de Sistema: no se crea ninguno nuevo.");
            return;
        }

        var normalizedPhone = PhoneNumber.Normalize(phone);
        PasswordPolicy.Validate(password);

        // Se comprueba incluyendo las cuentas borradas lógicamente: el índice único del
        // teléfono es global, así que una cuenta eliminada sigue ocupando su número y el
        // INSERT fallaría con un error de restricción en lugar de un mensaje entendible.
        var existing = await userRepository.GetByPhoneNumberIncludingDeletedAsync(normalizedPhone, cancellationToken);
        if (existing != null)
        {
            logger.LogWarning(
                "No se pudo crear el Administrador de Sistema inicial: el teléfono {Phone} ya pertenece a otra cuenta{Estado}. " +
                "Promuévela manualmente o usa otro número.",
                normalizedPhone, existing.IsDeleted ? " (eliminada)" : string.Empty);
            return;
        }

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var superAdmin = new User
        {
            Id = Guid.NewGuid(),
            FirstName = configuration["SUPERADMIN_FIRST_NAME"] ?? "Super",
            LastName = configuration["SUPERADMIN_LAST_NAME"] ?? "Admin",
            PhoneNumber = normalizedPhone,
            PasswordHash = passwordHasher.HashPassword(password),
            Role = "admin",
            CreatedAt = DateTime.UtcNow
        };

        await userRepository.AddAsync(superAdmin, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Administrador de Sistema inicial creado con el teléfono {Phone}. " +
            "Cambia su contraseña y elimina SUPERADMIN_PASSWORD del entorno.", normalizedPhone);
    }
}
