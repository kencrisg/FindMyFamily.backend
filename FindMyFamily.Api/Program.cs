using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using DotNetEnv;
using FindMyFamily.Api.Filters;
using FindMyFamily.Api.Hubs;
using FindMyFamily.Api.Middlewares;
using FindMyFamily.Api.Services;
using FindMyFamily.Api.Startup;
using FindMyFamily.Core;
using FindMyFamily.Core.Interfaces.Services;
using FindMyFamily.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

// 1. Carga variables de entorno desde .env
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// 2. Inyección de dependencias de capas Core e Infrastructure
builder.Services.AddCore();
builder.Services.AddInfrastructure(builder.Configuration);

// 3. SignalR y Notificador en Tiempo Real
builder.Services.AddSignalR();
builder.Services.AddScoped<ILocationRealtimeNotifier, SignalRLocationNotifier>();

// 4. Controladores y Swagger con soporte para JWT Bearer
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FindMyFamily API",
        Version = "v1",
        Description = "API REST y servidor de tiempo real para localización familiar bajo demanda (On-Demand)."
    });

    // Definición de seguridad Bearer
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingresa tu token JWT en el formato: Bearer {tu_token}"
    });

    // Agrega el candadito 🔒 y el requisito de seguridad exclusivamente a los endpoints con [Authorize]
    options.OperationFilter<AuthorizeCheckOperationFilter>();
});

// 5. Configuración de Autenticación JWT (con soporte para WebSockets / SignalR)
var jwtSecret = builder.Configuration["JWT_SECRET"]
    ?? throw new InvalidOperationException("JWT_SECRET is not configured.");

// El secreto se valida al arrancar: el valor publicado en .env.example estuvo en uso real,
// lo que permitía a cualquiera que leyera el repositorio firmar un token con rol de admin.
if (jwtSecret.Length < 32)
{
    throw new InvalidOperationException(
        "JWT_SECRET debe tener al menos 32 caracteres. Genera uno con: openssl rand -base64 48");
}

if (jwtSecret.Contains("CAMBIAR", StringComparison.OrdinalIgnoreCase)
    || jwtSecret.Contains("super_secret_jwt_key_find_my_family", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "JWT_SECRET sigue siendo el valor de ejemplo, que es público y por tanto está comprometido. " +
        "Genera uno propio con: openssl rand -base64 48");
}

var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? "FindMyFamilyApi";
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? "FindMyFamilyApp";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Fuera de desarrollo se exige metadata por HTTPS.
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Permite autenticar conexiones de SignalR vía Query String (?access_token=...)
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// 6. Rate limiting. Sin él, el login admitía fuerza bruta ilimitada, el alta de miembros
// servía para enumerar números de teléfono registrados y la petición de ubicación permitía
// mantener encendido el GPS de un familiar de forma continua.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            """{"error":true,"code":"TOO_MANY_REQUESTS","message":"Demasiadas solicitudes. Inténtalo de nuevo en unos momentos."}""",
            cancellationToken);
    };

    // Límite estricto para autenticación, por IP.
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ResolveClientKey(httpContext),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5)
        }));

    // Límite para peticiones de ubicación On-Demand, por usuario autenticado.
    options.AddPolicy("location-request", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ResolveClientKey(httpContext),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(5)
        }));

    // Límite global de seguridad para el resto de la API.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ResolveClientKey(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1)
            }));
});

var app = builder.Build();

// Crea el Administrador de Sistema inicial si no existe ninguno (idempotente).
await SuperAdminSeeder.SeedAsync(app.Services, app.Logger);

// 7. Middleware global de excepciones
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 8. Configuración de Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FindMyFamily API v1");
        c.RoutePrefix = "swagger";
    });
}
else
{
    // Fuera de desarrollo todo el tráfico va por TLS: por el canal viajan tokens,
    // contraseñas y coordenadas GPS.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 9. Endpoints de SignalR Hubs
app.MapHub<LocationHub>("/hubs/location");

app.MapGet("/", () => Results.Ok(new
{
    message = "FindMyFamily API is running",
    swagger = "/swagger",
    signalr = "/hubs/location"
}))
.WithName("Root")
.WithTags("Health");

app.Run();

// Particiona por usuario autenticado cuando lo hay, y por IP en caso contrario,
// para que un atacante no consuma la cuota de los usuarios legítimos.
static string ResolveClientKey(HttpContext httpContext)
{
    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!string.IsNullOrEmpty(userId))
    {
        return $"user:{userId}";
    }

    return $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}
