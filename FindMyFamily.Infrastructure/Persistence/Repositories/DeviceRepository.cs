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

    public async Task<Device?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .Include(d => d.User)
                .ThenInclude(u => u.FamilyMembers)
            .FirstOrDefaultAsync(d => d.RefreshTokenHash == refreshTokenHash, cancellationToken);
    }

    public async Task<Device?> GetByInstallationIdAsync(Guid userId, string installationId, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.InstallationId == installationId, cancellationToken);
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

    /// <summary>
    /// Marca como modificada SOLO la entidad raíz.
    /// `DbSet.Update()` marca todo el grafo cargado, y como las consultas traen los
    /// dispositivos y pertenencias con Include, al guardar reescribía esos valores desde
    /// la memoria. Eso resucitaba un refresh token que se acababa de revocar con
    /// ExecuteUpdate, dejando la sesión viva tras un cambio de contraseña.
    /// Si la entidad ya está rastreada, el seguimiento de cambios se encarga solo.
    /// </summary>
    public void Update(Device device)
    {
        var entry = _context.Entry(device);
        if (entry.State == EntityState.Detached)
        {
            entry.State = EntityState.Modified;
        }
    }

    public void Delete(Device device)
    {
        _context.Devices.Remove(device);
    }

    public async Task<int> DeactivateAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .Where(d => d.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(d => d.IsActive, false)
                    .SetProperty(d => d.FcmToken, (string?)null)
                    .SetProperty(d => d.RefreshTokenHash, (string?)null)
                    .SetProperty(d => d.RefreshTokenExpiry, (DateTime?)null),
                cancellationToken);
    }

    public async Task<int> RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .Where(d => d.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(d => d.RefreshTokenHash, (string?)null)
                    .SetProperty(d => d.RefreshTokenExpiry, (DateTime?)null),
                cancellationToken);
    }
}
