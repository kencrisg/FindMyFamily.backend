using FindMyFamily.Core.DTOs.Locations;
using FindMyFamily.Core.Entities;
using FindMyFamily.Core.Exceptions;
using FindMyFamily.Core.Interfaces.Repositories;
using FindMyFamily.Core.Interfaces.Services;

namespace FindMyFamily.Core.Services;

public class LocationService : ILocationService
{
    /// <summary>Ventana durante la cual una petición On-Demand admite un reporte.</summary>
    private static readonly TimeSpan RequestValidity = TimeSpan.FromMinutes(10);

    /// <summary>Tolerancia de reloj aceptada en el `capturedAt` que envía el cliente.</summary>
    private static readonly TimeSpan FutureSkewTolerance = TimeSpan.FromMinutes(5);

    /// <summary>Antigüedad máxima admitida en un reporte: más viejo que esto no es "ubicación actual".</summary>
    private static readonly TimeSpan MaxReportAge = TimeSpan.FromHours(24);

    // Tope de peticiones por par (solicitante, objetivo) en la ventana indicada. Sin esto,
    // un miembro podía disparar peticiones en bucle y mantener el GPS del familiar encendido
    // de forma continua: exactamente lo que el modelo On-Demand existe para evitar.
    private const int MaxRequestsPerWindow = 10;
    private static readonly TimeSpan RequestRateWindow = TimeSpan.FromMinutes(10);

    private const int MaxHistoryLimit = 500;

    private readonly ILocationRepository _locationRepository;
    private readonly ILocationRequestRepository _locationRequestRepository;
    private readonly IFamilyRepository _familyRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ILocationRealtimeNotifier _realtimeNotifier;

    public LocationService(
        ILocationRepository locationRepository,
        ILocationRequestRepository locationRequestRepository,
        IFamilyRepository familyRepository,
        IDeviceRepository deviceRepository,
        IUnitOfWork unitOfWork,
        IPushNotificationService pushNotificationService,
        ILocationRealtimeNotifier realtimeNotifier)
    {
        _locationRepository = locationRepository;
        _locationRequestRepository = locationRequestRepository;
        _familyRepository = familyRepository;
        _deviceRepository = deviceRepository;
        _unitOfWork = unitOfWork;
        _pushNotificationService = pushNotificationService;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<LocationRequestResponseDto> RequestLocationAsync(Guid requesterId, RequestLocationDto request, CancellationToken cancellationToken = default)
    {
        if (request.FamilyId == Guid.Empty || request.TargetUserId == Guid.Empty)
        {
            throw new BadRequestException("Debes indicar la familia y el familiar a localizar.", "INVALID_INPUT");
        }

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

        // 3. Limitar la frecuencia de peticiones sobre un mismo familiar
        var since = DateTime.UtcNow.Subtract(RequestRateWindow);
        var recentCount = await _locationRequestRepository.CountRecentByRequesterAsync(
            requesterId, request.TargetUserId, since, cancellationToken);

        if (recentCount >= MaxRequestsPerWindow)
        {
            throw new AppTooManyRequestsException(
                "Has solicitado la ubicación de este familiar demasiadas veces. Espera unos minutos.",
                "LOCATION_REQUEST_RATE_LIMITED");
        }

        // 4. Última ubicación conocida. Se resuelve siempre, porque forma parte de la
        //    respuesta tanto si el dispositivo responde como si está inalcanzable.
        var lastKnown = await GetLastKnownAsync(request.TargetUserId, cancellationToken);

        // 5. Obtener el dispositivo activo con FCM token del usuario destino.
        //    Si no hay ninguno, el familiar es inalcanzable: NO es un error de la petición,
        //    así que se responde 200 con estado UNAVAILABLE y la última posición conocida.
        //    Antes esto lanzaba un 404 y la app no tenía con qué construir el mensaje de
        //    "no disponible, su última ubicación fue a las …".
        var targetDevice = await _deviceRepository.GetLatestActiveDeviceByUserIdAsync(request.TargetUserId, cancellationToken);
        if (targetDevice == null || string.IsNullOrWhiteSpace(targetDevice.FcmToken))
        {
            return Unavailable(request.TargetUserId, lastKnown,
                "El dispositivo de tu familiar no está disponible en este momento.");
        }

        // 6. Registrar la petición ANTES de enviar el push: deja rastro de quién pidió qué
        //    y es lo que autoriza y acota el posterior reporte de coordenadas.
        var now = DateTime.UtcNow;
        var locationRequest = new LocationRequest
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            TargetUserId = request.TargetUserId,
            FamilyId = request.FamilyId,
            CreatedAt = now,
            ExpiresAt = now.Add(RequestValidity)
        };

        await _locationRequestRepository.AddAsync(locationRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 7. Emitir el Silent Push a través de Firebase Messaging
        var sent = await _pushNotificationService.SendSilentLocationRequestAsync(
            targetDevice.FcmToken,
            locationRequest.Id,
            requesterId,
            request.FamilyId,
            cancellationToken
        );

        if (!sent)
        {
            // El envío falló (token caducado, Firebase caído, credenciales ausentes).
            // Desde el punto de vista del usuario es lo mismo que estar offline.
            return Unavailable(request.TargetUserId, lastKnown,
                "No se pudo contactar con el dispositivo de tu familiar en este momento.");
        }

        return new LocationRequestResponseDto(
            RequestId: locationRequest.Id,
            TargetUserId: request.TargetUserId,
            Status: LocationRequestStatus.Requested,
            Message: "Solicitud enviada. Esperando la ubicación de tu familiar…",
            LastKnownLocation: lastKnown
        );
    }

    private static LocationRequestResponseDto Unavailable(Guid targetUserId, LocationDto? lastKnown, string message)
    {
        var detalle = lastKnown == null
            ? " Todavía no hay ninguna ubicación registrada suya."
            : " Te mostramos su última ubicación conocida.";

        return new LocationRequestResponseDto(
            RequestId: null,
            TargetUserId: targetUserId,
            Status: LocationRequestStatus.Unavailable,
            Message: message + detalle,
            LastKnownLocation: lastKnown
        );
    }

    private async Task<LocationDto?> GetLastKnownAsync(Guid userId, CancellationToken cancellationToken)
    {
        var location = await _locationRepository.GetLatestByUserIdAsync(userId, cancellationToken);
        return location == null ? null : ToDto(location);
    }

    private static LocationDto ToDto(Location location) => new(
        Id: location.Id,
        UserId: location.UserId,
        Latitude: location.Latitude,
        Longitude: location.Longitude,
        CapturedAt: location.CapturedAt
    );

    public async Task<LocationDto> ReportLocationAsync(Guid reportingUserId, ReportLocationDto request, CancellationToken cancellationToken = default)
    {
        if (request.Latitude is null || request.Longitude is null)
        {
            throw new BadRequestException("La latitud y la longitud son obligatorias.", "INVALID_COORDINATES");
        }

        var latitude = request.Latitude.Value;
        var longitude = request.Longitude.Value;

        if (latitude < -90m || latitude > 90m || longitude < -180m || longitude > 180m)
        {
            throw new BadRequestException("Coordenadas geográficas inválidas.", "INVALID_COORDINATES");
        }

        var now = DateTime.UtcNow;
        var capturedAt = NormalizeCapturedAt(request.CapturedAt, now);

        var location = new Location
        {
            Id = Guid.NewGuid(),
            UserId = reportingUserId,
            Latitude = latitude,
            Longitude = longitude,
            CapturedAt = capturedAt
        };

        await _locationRepository.AddAsync(location, cancellationToken);

        // La difusión se acota a la familia que originó la petición. Antes se emitía a TODAS
        // las familias del usuario, de modo que una petición hecha en un círculo familiar
        // filtraba la coordenada a los demás círculos.
        var pending = await _locationRequestRepository.GetPendingForTargetAsync(reportingUserId, now, cancellationToken);

        var recipientIds = new HashSet<Guid>();

        foreach (var pendingRequest in pending)
        {
            // Se revalida la pertenencia en el momento del reporte: entre la petición y la
            // respuesta el solicitante pudo haber sido expulsado de la familia.
            var stillMember = await _familyRepository.IsMemberAsync(
                pendingRequest.FamilyId, pendingRequest.RequesterId, cancellationToken);

            if (!stillMember)
            {
                continue;
            }

            recipientIds.Add(pendingRequest.RequesterId);

            pendingRequest.FulfilledAt = now;
            pendingRequest.LocationId = location.Id;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var locationDto = ToDto(location);

        // El propio usuario siempre recibe su actualización (sincronización entre sus dispositivos).
        recipientIds.Add(reportingUserId);

        await _realtimeNotifier.SendLocationUpdateAsync(recipientIds.ToList(), locationDto, cancellationToken);

        return locationDto;
    }

    public async Task<LocationDto?> GetLatestLocationAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        await ValidateFamilyAccessAsync(requestingUserId, familyId, targetUserId, cancellationToken);

        return await GetLastKnownAsync(targetUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<LocationDto>> GetLocationHistoryAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, int limit = 50, CancellationToken cancellationToken = default)
    {
        await ValidateFamilyAccessAsync(requestingUserId, familyId, targetUserId, cancellationToken);

        // Acotar el límite: un valor negativo hacía que PostgreSQL rechazara el LIMIT (500)
        // y un valor enorme volcaba el historial completo en una sola respuesta.
        var safeLimit = Math.Clamp(limit, 1, MaxHistoryLimit);

        var history = await _locationRepository.GetHistoryByUserIdAsync(targetUserId, safeLimit, cancellationToken);

        return history.Select(ToDto).ToList();
    }

    public async Task<int> DeleteMyHistoryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var deleted = await _locationRepository.DeleteAllByUserIdAsync(userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return deleted;
    }

    /// <summary>
    /// Normaliza la marca temporal aportada por el cliente. Dos motivos:
    /// la columna es `timestamp with time zone` y Npgsql rechaza un DateTime cuyo Kind no sea Utc
    /// (un ISO sin zona producía un 500); y una fecha futura quedaba fijada como "última ubicación"
    /// de forma permanente, porque la consulta ordena por captured_at descendente.
    /// </summary>
    private static DateTime NormalizeCapturedAt(DateTime? capturedAt, DateTime now)
    {
        if (capturedAt is null)
        {
            return now;
        }

        var value = capturedAt.Value;

        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            // Sin zona horaria explícita se interpreta como UTC, que es lo que el cliente envía.
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        if (utc > now.Add(FutureSkewTolerance))
        {
            throw new BadRequestException(
                "La marca temporal de la ubicación no puede estar en el futuro.", "INVALID_CAPTURED_AT");
        }

        if (utc < now.Subtract(MaxReportAge))
        {
            throw new BadRequestException(
                "La marca temporal de la ubicación es demasiado antigua.", "INVALID_CAPTURED_AT");
        }

        return utc;
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
