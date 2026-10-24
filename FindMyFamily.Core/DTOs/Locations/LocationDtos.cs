namespace FindMyFamily.Core.DTOs.Locations;

public record RequestLocationDto(
    Guid TargetUserId,
    Guid FamilyId
);

// Latitud y longitud son anulables a propósito: siendo `decimal` no anulable, omitirlas
// en el JSON daba 0,0 ("Null Island") guardado como una ubicación válida en lugar de un 400.
public record ReportLocationDto(
    decimal? Latitude,
    decimal? Longitude,
    DateTime? CapturedAt = null
);

public record LocationDto(
    Guid Id,
    Guid UserId,
    decimal Latitude,
    decimal Longitude,
    DateTime CapturedAt
);

/// <summary>Estado de una petición de ubicación On-Demand.</summary>
public static class LocationRequestStatus
{
    /// <summary>El Silent Push salió hacia el dispositivo; se espera el reporte por SignalR.</summary>
    public const string Requested = "REQUESTED";

    /// <summary>
    /// No se pudo contactar con el dispositivo (sin dispositivo activo, sin token de
    /// notificación, o el envío falló). La app debe mostrar "no disponible" junto con
    /// la última ubicación conocida, si existe.
    /// </summary>
    public const string Unavailable = "UNAVAILABLE";
}

/// <summary>
/// Respuesta a una petición de ubicación. Incluye siempre la última ubicación conocida
/// (cuando existe) para que la app pueda pintar algo en el mapa de inmediato: mientras
/// llega el reporte en vivo si el dispositivo responde, o como único dato disponible si
/// el familiar está apagado o sin conexión.
/// </summary>
public record LocationRequestResponseDto(
    Guid? RequestId,
    Guid TargetUserId,
    string Status,
    string Message,
    LocationDto? LastKnownLocation
);
