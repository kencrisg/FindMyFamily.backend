using FindMyFamily.Core.Interfaces.Repositories;
using FindMyFamily.Core.Interfaces.Services;
using FindMyFamily.Infrastructure.Persistence;
using FindMyFamily.Infrastructure.Persistence.Repositories;
using FindMyFamily.Infrastructure.Security;
using FindMyFamily.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FindMyFamily.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Base de datos (PostgreSQL)
        var connectionString = configuration.GetConnectionString("DefaultConnection");

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
                    "No hay configuración de base de datos. Define ConnectionStrings__DefaultConnection " +
                    "o bien DB_HOST, DB_NAME, DB_USER y DB_PASSWORD en el entorno.");
            }

            connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

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
}
