using FindMyFamily.Core.Entities;

namespace FindMyFamily.Core.Interfaces.Repositories;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Device?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken = default);
    Task<Device?> GetByInstallationIdAsync(Guid userId, string installationId, CancellationToken cancellationToken = default);
    Task<Device?> GetLatestActiveDeviceByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Device device, CancellationToken cancellationToken = default);
    void Update(Device device);
    void Delete(Device device);

    /// <summary>Revoca el refresh token de todos los dispositivos del usuario.</summary>
    Task<int> RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Desactiva los dispositivos del usuario (al borrar su cuenta lógicamente).</summary>
    Task<int> DeactivateAllAsync(Guid userId, CancellationToken cancellationToken = default);
}
