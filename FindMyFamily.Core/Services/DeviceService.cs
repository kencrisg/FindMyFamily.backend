using FindMyFamily.Core.DTOs.Devices;
using FindMyFamily.Core.Entities;
using FindMyFamily.Core.Exceptions;
using FindMyFamily.Core.Interfaces.Repositories;
using FindMyFamily.Core.Interfaces.Services;

namespace FindMyFamily.Core.Services;

public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeviceService(IDeviceRepository deviceRepository, IUnitOfWork unitOfWork)
    {
        _deviceRepository = deviceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task UpdateFcmTokenAsync(Guid userId, Guid? deviceId, UpdateFcmTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FcmToken))
        {
            throw new BadRequestException("El token de FCM es obligatorio.", "INVALID_INPUT");
        }

        Device? device = null;

        if (deviceId.HasValue)
        {
            device = await _deviceRepository.GetByIdAsync(deviceId.Value, cancellationToken);
            if (device != null && device.UserId != userId)
            {
                throw new ForbiddenException("No tienes permiso para modificar este dispositivo.", "FORBIDDEN");
            }
        }

        if (device == null)
        {
            device = await _deviceRepository.GetLatestActiveDeviceByUserIdAsync(userId, cancellationToken);
        }

        if (device == null)
        {
            // Si el dispositivo aún no existía, creamos el registro activo para este usuario
            device = new Device
            {
                Id = deviceId ?? Guid.NewGuid(),
                UserId = userId,
                FcmToken = request.FcmToken.Trim(),
                LastActiveAt = DateTime.UtcNow,
                IsActive = true
            };
            await _deviceRepository.AddAsync(device, cancellationToken);
        }
        else
        {
            device.FcmToken = request.FcmToken.Trim();
            device.LastActiveAt = DateTime.UtcNow;
            device.IsActive = true;
            _deviceRepository.Update(device);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
