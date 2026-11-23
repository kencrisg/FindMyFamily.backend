using FindMyFamily.Core.Entities;

namespace FindMyFamily.Core.Interfaces.Repositories;

public interface ILocationRequestRepository
{
    Task AddAsync(LocationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Peticiones vigentes (no cumplidas y no expiradas) dirigidas a este usuario,
    /// de la más reciente a la más antigua.
    /// </summary>
    Task<IReadOnlyList<LocationRequest>> GetPendingForTargetAsync(Guid targetUserId, DateTime now, CancellationToken cancellationToken = default);

    Task<int> CountRecentByRequesterAsync(Guid requesterId, Guid targetUserId, DateTime since, CancellationToken cancellationToken = default);
}
