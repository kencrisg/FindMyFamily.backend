using FindMyFamily.Core.DTOs.Locations;

namespace FindMyFamily.Core.Interfaces.Services;

public interface ILocationRealtimeNotifier
{
    /// <summary>
    /// Notifica la nueva coordenada GPS vía SignalR únicamente a los usuarios indicados.
    /// Se dirige a destinatarios concretos (y no a un grupo "familia") porque la pertenencia
    /// a los grupos de SignalR se fijaba al conectar: un miembro expulsado seguía recibiendo
    /// ubicaciones mientras mantuviera el socket abierto.
    /// </summary>
    Task SendLocationUpdateAsync(
        IReadOnlyList<Guid> recipientUserIds,
        LocationDto location,
        CancellationToken cancellationToken = default);
}
