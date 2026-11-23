using FindMyFamily.Api.Hubs;
using FindMyFamily.Core.DTOs.Locations;
using FindMyFamily.Core.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;

namespace FindMyFamily.Api.Services;

public class SignalRLocationNotifier : ILocationRealtimeNotifier
{
    /// <summary>
    /// Nombre del evento que escucha el cliente móvil. Debe coincidir exactamente con el
    /// manejador registrado en `signalr-location.service.ts`.
    /// </summary>
    private const string LocationUpdatedEvent = "LocationUpdated";

    private readonly IHubContext<LocationHub> _hubContext;
    private readonly ILogger<SignalRLocationNotifier> _logger;

    public SignalRLocationNotifier(IHubContext<LocationHub> hubContext, ILogger<SignalRLocationNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendLocationUpdateAsync(
        IReadOnlyList<Guid> recipientUserIds,
        LocationDto location,
        CancellationToken cancellationToken = default)
    {
        if (recipientUserIds.Count == 0)
        {
            return;
        }

        var groups = recipientUserIds.Select(id => $"user_{id}").ToList();

        await _hubContext.Clients.Groups(groups)
            .SendAsync(LocationUpdatedEvent, location, cancellationToken);

        _logger.LogInformation(
            "Ubicación de usuario {UserId} retransmitida en tiempo real a {Count} destinatarios autorizados",
            location.UserId, recipientUserIds.Count);
    }
}
