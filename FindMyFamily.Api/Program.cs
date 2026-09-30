using System.Text;
using DotNetEnv;
using FindMyFamily.Api.Middlewares;
using FindMyFamily.Core;
using FindMyFamily.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

// 1. Carga variables de entorno desde .env
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// 2. Inyección de dependencias de capas Core e Infrastructure
builder.Services.AddCore();
builder.Services.AddInfrastructure(builder.Configuration);

// 3. Controladores y Swagger con soporte para JWT Bearer
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

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingresa tu token JWT en el formato: Bearer {tu_token}"
    });

    var securityRequirement = new OpenApiSecurityRequirement();
    securityRequirement.Add(new OpenApiSecuritySchemeReference("Bearer"), new List<string>());
    options.AddSecurityRequirement(doc => securityRequirement);
});

// 4. Configuración de Autenticación JWT
var jwtSecret = builder.Configuration["JWT_SECRET"]
    ?? throw new InvalidOperationException("JWT_SECRET is not configured.");
var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? "FindMyFamilyApi";
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? "FindMyFamilyApp";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// 5. Middleware global de excepciones
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 6. Configuración de Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FindMyFamily API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Ok(new { 
    message = "FindMyFamily API is running",
    swagger = "/swagger"
}))
.WithName("Root");

app.Run();
