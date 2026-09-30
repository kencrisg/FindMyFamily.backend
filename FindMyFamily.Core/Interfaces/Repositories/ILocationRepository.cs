using FindMyFamily.Core.Entities;

namespace FindMyFamily.Core.Interfaces.Repositories;

public interface ILocationRepository
{
    Task<Location?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Location>> GetHistoryByUserIdAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default);
    Task AddAsync(Location location, CancellationToken cancellationToken = default);
}
