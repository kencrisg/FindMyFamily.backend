using FindMyFamily.Core.DTOs.Locations;
using FindMyFamily.Core.Entities;
using FindMyFamily.Core.Exceptions;
using FindMyFamily.Core.Interfaces.Repositories;
using FindMyFamily.Core.Interfaces.Services;

namespace FindMyFamily.Core.Services;

public class LocationService : ILocationService
{
    private readonly ILocationRepository _locationRepository;
    private readonly IFamilyRepository _familyRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ILocationRealtimeNotifier _realtimeNotifier;

    public LocationService(
        ILocationRepository locationRepository,
        IFamilyRepository familyRepository,
        IDeviceRepository deviceRepository,
        IUnitOfWork unitOfWork,
        IPushNotificationService pushNotificationService,
        ILocationRealtimeNotifier realtimeNotifier)
    {
        _locationRepository = locationRepository;
        _familyRepository = familyRepository;
        _deviceRepository = deviceRepository;
        _unitOfWork = unitOfWork;
        _pushNotificationService = pushNotificationService;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<LocationRequestResponseDto> RequestLocationAsync(Guid requesterId, RequestLocationDto request, CancellationToken cancellationToken = default)
    {
        // 1. Validar que el solicitante sea miembro de la familia
        var requesterIsMember = await _familyRepository.IsMemberAsync(request.FamilyId, requesterId, cancellationToken);
        if (!requesterIsMember)
        {
            throw new ForbiddenException("No perteneces al grupo familiar especificado.", "FORBIDDEN");
        }

        // 2. Validar que el usuario objetivo también sea miembro de la familia
        var targetIsMember = await _familyRepository.IsMemberAsync(request.FamilyId, request.TargetUserId, cancellationToken);
        if (!targetIsMember)
        {
            throw new ForbiddenException("El usuario objetivo no pertenece al grupo familiar especificado.", "FORBIDDEN");
        }

        // 3. Obtener el dispositivo activo con FCM token del usuario destino
        var targetDevice = await _deviceRepository.GetLatestActiveDeviceByUserIdAsync(request.TargetUserId, cancellationToken);
        if (targetDevice == null || string.IsNullOrWhiteSpace(targetDevice.FcmToken))
        {
            throw new NotFoundException("El dispositivo del familiar no se encuentra activo o no tiene un token de notificación registrado.", "DEVICE_NOT_FOUND");
        }

        // 4. Emitir el Silent Push a través de Firebase Messaging
        var requestId = Guid.NewGuid();
        var sent = await _pushNotificationService.SendSilentLocationRequestAsync(
            targetDevice.FcmToken,
            requestId,
            requesterId,
            request.FamilyId,
            cancellationToken
        );

        if (!sent)
        {
            throw new BadRequestException("No se pudo enviar la notificación de localización al dispositivo destino.", "FCM_SEND_FAILED");
        }

        return new LocationRequestResponseDto(
            RequestId: requestId,
            TargetUserId: request.TargetUserId,
            Status: "REQUESTED",
            Message: "Solicitud On-Demand enviada al dispositivo destino exitosamente."
        );
    }

    public async Task<LocationDto> ReportLocationAsync(Guid reportingUserId, ReportLocationDto request, CancellationToken cancellationToken = default)
    {
        // Validar rango de coordenadas GPS
        if (request.Latitude < -90m || request.Latitude > 90m || request.Longitude < -180m || request.Longitude > 180m)
        {
            throw new BadRequestException("Coordenadas geográficas inválidas.", "INVALID_COORDINATES");
        }

        var location = new Location
        {
            Id = Guid.NewGuid(),
            UserId = reportingUserId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            CapturedAt = request.CapturedAt ?? DateTime.UtcNow
        };

        await _locationRepository.AddAsync(location, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var locationDto = new LocationDto(
            Id: location.Id,
            UserId: location.UserId,
            Latitude: location.Latitude,
            Longitude: location.Longitude,
            CapturedAt: location.CapturedAt
        );

        // Obtener los grupos familiares a los que pertenece el usuario para emitir en tiempo real
        var userFamilies = await _familyRepository.GetFamiliesByUserIdAsync(reportingUserId, cancellationToken);
        var familyIds = userFamilies.Select(f => f.Id).ToList();

        // Transmisión en tiempo real vía WebSockets / SignalR
        await _realtimeNotifier.SendLocationUpdateAsync(reportingUserId, familyIds, locationDto, cancellationToken);

        return locationDto;
    }

    public async Task<LocationDto?> GetLatestLocationAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        await ValidateFamilyAccessAsync(requestingUserId, familyId, targetUserId, cancellationToken);

        var location = await _locationRepository.GetLatestByUserIdAsync(targetUserId, cancellationToken);
        if (location == null) return null;

        return new LocationDto(
            Id: location.Id,
            UserId: location.UserId,
            Latitude: location.Latitude,
            Longitude: location.Longitude,
            CapturedAt: location.CapturedAt
        );
    }

    public async Task<IReadOnlyList<LocationDto>> GetLocationHistoryAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, int limit = 50, CancellationToken cancellationToken = default)
    {
        await ValidateFamilyAccessAsync(requestingUserId, familyId, targetUserId, cancellationToken);

        var history = await _locationRepository.GetHistoryByUserIdAsync(targetUserId, limit, cancellationToken);

        return history.Select(l => new LocationDto(
            Id: l.Id,
            UserId: l.UserId,
            Latitude: l.Latitude,
            Longitude: l.Longitude,
            CapturedAt: l.CapturedAt
        )).ToList();
    }

    private async Task ValidateFamilyAccessAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var requesterIsMember = await _familyRepository.IsMemberAsync(familyId, requestingUserId, cancellationToken);
        if (!requesterIsMember)
        {
            throw new ForbiddenException("No tienes acceso a este grupo familiar.", "FORBIDDEN");
        }

        var targetIsMember = await _familyRepository.IsMemberAsync(familyId, targetUserId, cancellationToken);
        if (!targetIsMember)
        {
            throw new ForbiddenException("El usuario solicitado no pertenece a este grupo familiar.", "FORBIDDEN");
        }
    }
}
