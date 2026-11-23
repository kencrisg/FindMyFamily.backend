using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FindMyFamily.Api.Hubs;

[Authorize]
public class LocationHub : Hub
{
    private readonly ILogger<LocationHub> _logger;

    public LocationHub(ILogger<LocationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        // La conexión se suscribe ÚNICAMENTE a su propio canal. Antes también se unía a un
        // grupo por familia, y esa pertenencia quedaba congelada en el momento de conectar:
        // un miembro expulsado seguía recibiendo las ubicaciones del grupo mientras mantuviera
        // el socket abierto. Ahora quién recibe cada actualización se decide en el emisor,
        // consultando la pertenencia vigente en base de datos.
        if (Guid.TryParse(userIdStr, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("Usuario {UserId} conectado a LocationHub (ConnectionId: {ConnId})", userId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Conexión {ConnId} desconectada de LocationHub", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
