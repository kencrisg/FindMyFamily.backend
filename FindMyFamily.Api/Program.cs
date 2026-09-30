using DotNetEnv;
using FindMyFamily.Infrastructure;

// Carga variables del archivo .env al entorno antes de inicializar la aplicación
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/", () => Results.Ok(new { message = "FindMyFamily API is running" }))
   .WithName("Root");

app.Run();
