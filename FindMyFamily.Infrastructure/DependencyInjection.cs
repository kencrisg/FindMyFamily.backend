using FindMyFamily.Core.Interfaces.Repositories;
using FindMyFamily.Core.Interfaces.Services;
using FindMyFamily.Infrastructure.Persistence;
using FindMyFamily.Infrastructure.Persistence.Repositories;
using FindMyFamily.Infrastructure.Security;
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
            var host = configuration["DB_HOST"] ?? "localhost";
            var port = configuration["DB_PORT"] ?? "5432";
            var database = configuration["DB_NAME"] ?? "findmyfamilydb";
            var username = configuration["DB_USER"] ?? "postgres";
            var password = configuration["DB_PASSWORD"] ?? "passwordpostgres";

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

        // 3. Servicios de Seguridad e Infraestructura
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}
