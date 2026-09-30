using FindMyFamily.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FindMyFamily.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Obtener cadena de conexión (bien sea desde appsettings o desde la variable ConnectionStrings__DefaultConnection)
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // 2. Si no viene completa, construirla a partir de las variables de entorno DB_*
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

        return services;
    }
}
