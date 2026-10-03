using FindMyFamily.Core.DTOs.Devices;

namespace FindMyFamily.Core.Interfaces.Services;

public interface IDeviceService
{
    Task UpdateFcmTokenAsync(Guid userId, Guid? deviceId, UpdateFcmTokenRequestDto request, CancellationToken cancellationToken = default);
}
