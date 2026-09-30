using System.Security.Claims;
using FindMyFamily.Core.Interfaces.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FindMyFamily.Api.Hubs;

[Authorize]
public class LocationHub : Hub
{
    private readonly IFamilyRepository _familyRepository;
    private readonly ILogger<LocationHub> _logger;

    public LocationHub(IFamilyRepository familyRepository, ILogger<LocationHub> logger)
    {
        _familyRepository = familyRepository;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        if (Guid.TryParse(userIdStr, out var userId))
        {
            // 1. Unir la conexión al canal privado del usuario
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

            // 2. Unir la conexión a todos los canales de sus grupos familiares
            var families = await _familyRepository.GetFamiliesByUserIdAsync(userId);
            foreach (var family in families)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"family_{family.Id}");
            }

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
