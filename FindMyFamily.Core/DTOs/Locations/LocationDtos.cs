namespace FindMyFamily.Core.DTOs.Locations;

public record RequestLocationDto(
    Guid TargetUserId,
    Guid FamilyId
);

public record ReportLocationDto(
    decimal Latitude,
    decimal Longitude,
    DateTime? CapturedAt = null
);

public record LocationDto(
    Guid Id,
    Guid UserId,
    decimal Latitude,
    decimal Longitude,
    DateTime CapturedAt
);

public record LocationRequestResponseDto(
    Guid RequestId,
    Guid TargetUserId,
    string Status,
    string Message
);
