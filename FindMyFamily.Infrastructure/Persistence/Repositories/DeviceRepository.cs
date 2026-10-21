using FindMyFamily.Core.Entities;
using FindMyFamily.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FindMyFamily.Infrastructure.Persistence.Repositories;

public class DeviceRepository : IDeviceRepository
{
    private readonly ApplicationDbContext _context;

    public DeviceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<Device?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.RefreshToken == refreshToken, cancellationToken);
    }

    public async Task<IReadOnlyList<Device>> GetActiveDevicesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .Where(d => d.UserId == userId && d.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<Device?> GetLatestActiveDeviceByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .Where(d => d.UserId == userId && d.IsActive && !string.IsNullOrEmpty(d.FcmToken))
            .OrderByDescending(d => d.LastActiveAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        await _context.Devices.AddAsync(device, cancellationToken);
    }

    public void Update(Device device)
    {
        _context.Devices.Update(device);
    }

    public void Delete(Device device)
    {
        _context.Devices.Remove(device);
    }
}
