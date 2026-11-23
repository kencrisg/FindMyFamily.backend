using FindMyFamily.Core.Interfaces.Repositories;
using FindMyFamily.Core.Interfaces.Services;
using FindMyFamily.Infrastructure.Persistence;
using FindMyFamily.Infrastructure.Persistence.Repositories;
using FindMyFamily.Infrastructure.Security;
using FindMyFamily.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FindMyFamily.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Base de datos (PostgreSQL)
        // Neon (y la mayoría de proveedores gestionados) entrega la cadena como URL
        // postgresql://usuario:clave@host/db?sslmode=require. Npgsql espera el formato
        // clave=valor, así que se normaliza aquí: pegar la URL tal cual es el error más
        // habitual al desplegar y el mensaje que devuelve Npgsql no lo explica.
        var connectionString = NormalizeConnectionString(
            configuration.GetConnectionString("DefaultConnection")
            ?? configuration["DATABASE_URL"]);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var host = configuration["DB_HOST"];
            var port = configuration["DB_PORT"] ?? "5432";
            var database = configuration["DB_NAME"];
            var username = configuration["DB_USER"];
            var password = configuration["DB_PASSWORD"];

            // Sin credenciales por defecto: antes se caía a user/password, lo que arrancaba
            // la aplicación con una configuración silenciosamente equivocada.
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database)
                || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "No hay configuración de base de datos. Define ConnectionStrings__DefaultConnection, " +
                    "DATABASE_URL o bien DB_HOST, DB_NAME, DB_USER y DB_PASSWORD en el entorno.");
            }

            connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
        }

        // SingleQuery explícito: es lo que EF ya hacía por defecto, pero sin declararlo avisaba
        // en cada consulta con varios Include de colecciones. Con un puñado de dispositivos y
        // familias por usuario, una sola consulta es más rápida que partirla en varias.
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)));

        // 2. Repositorios y Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IFamilyRepository, FamilyRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<ILocationRequestRepository, LocationRequestRepository>();

        // 3. Servicios de Seguridad e Infraestructura
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IRefreshTokenHasher, Sha256RefreshTokenHasher>();
        services.AddSingleton<IPushNotificationService, FirebasePushNotificationService>();

        return services;
    }

    /// <summary>
    /// Convierte una cadena de conexión en formato URL (postgresql://…) al formato
    /// clave=valor que entiende Npgsql. Si ya viene en ese formato, la devuelve intacta.
    /// </summary>
    private static string? NormalizeConnectionString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();

        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var uri = new Uri(trimmed);
        var userInfo = uri.UserInfo.Split(':', 2);

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            // Uri no conoce el esquema postgresql, así que sin puerto explícito devuelve -1.
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            // Neon exige TLS. Require cifra la conexión sin fijar el certificado: suficiente
            // aquí y evita tener que distribuir la CA del proveedor en la imagen.
            SslMode = SslMode.Require
        }.ConnectionString;
    }
}
