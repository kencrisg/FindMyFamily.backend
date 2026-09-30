using FindMyFamily.Core.Entities;

namespace FindMyFamily.Core.Interfaces.Repositories;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Device?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Device>> GetActiveDevicesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Device?> GetLatestActiveDeviceByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Device device, CancellationToken cancellationToken = default);
    void Update(Device device);
    void Delete(Device device);
}
