using FindMyFamily.Core.Interfaces.Services;
using FindMyFamily.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FindMyFamily.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IFamilyService, FamilyService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
