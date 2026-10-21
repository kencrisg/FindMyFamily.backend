using FindMyFamily.Core.DTOs.Locations;

namespace FindMyFamily.Core.Interfaces.Services;

public interface ILocationRealtimeNotifier
{
    /// <summary>
    /// Notifica en tiempo real a los grupos familiares del usuario la nueva coordenada GPS vía SignalR.
    /// </summary>
    Task SendLocationUpdateAsync(Guid userId, IReadOnlyList<Guid> familyIds, LocationDto location, CancellationToken cancellationToken = default);
}
