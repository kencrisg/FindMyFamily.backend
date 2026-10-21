using FindMyFamily.Api.Hubs;
using FindMyFamily.Core.DTOs.Locations;
using FindMyFamily.Core.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;

namespace FindMyFamily.Api.Services;

public class SignalRLocationNotifier : ILocationRealtimeNotifier
{
    private readonly IHubContext<LocationHub> _hubContext;
    private readonly ILogger<SignalRLocationNotifier> _logger;

    public SignalRLocationNotifier(IHubContext<LocationHub> hubContext, ILogger<SignalRLocationNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendLocationUpdateAsync(Guid userId, IReadOnlyList<Guid> familyIds, LocationDto location, CancellationToken cancellationToken = default)
    {
        // 1. Enviar evento al canal privado del usuario
        await _hubContext.Clients.Group($"user_{userId}")
            .SendAsync("LocationUpdated", location, cancellationToken);

        // 2. Enviar evento a todos los grupos familiares
        foreach (var familyId in familyIds)
        {
            await _hubContext.Clients.Group($"family_{familyId}")
                .SendAsync("LocationUpdated", location, cancellationToken);
        }

        _logger.LogInformation("Ubicación de usuario {UserId} retransmitida en tiempo real a {Count} grupos familiares",
            userId, familyIds.Count);
    }
}
