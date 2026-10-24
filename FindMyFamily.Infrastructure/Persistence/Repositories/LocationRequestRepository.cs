using FindMyFamily.Core.Entities;
using FindMyFamily.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FindMyFamily.Infrastructure.Persistence.Repositories;

public class LocationRequestRepository : ILocationRequestRepository
{
    private readonly ApplicationDbContext _context;

    public LocationRequestRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(LocationRequest request, CancellationToken cancellationToken = default)
    {
        await _context.LocationRequests.AddAsync(request, cancellationToken);
    }

    public async Task<IReadOnlyList<LocationRequest>> GetPendingForTargetAsync(Guid targetUserId, DateTime now, CancellationToken cancellationToken = default)
    {
        return await _context.LocationRequests
            .Where(lr => lr.TargetUserId == targetUserId
                         && lr.FulfilledAt == null
                         && lr.ExpiresAt >= now)
            .OrderByDescending(lr => lr.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountRecentByRequesterAsync(Guid requesterId, Guid targetUserId, DateTime since, CancellationToken cancellationToken = default)
    {
        return await _context.LocationRequests
            .AsNoTracking()
            .CountAsync(lr => lr.RequesterId == requesterId
                              && lr.TargetUserId == targetUserId
                              && lr.CreatedAt >= since,
                        cancellationToken);
    }
}
