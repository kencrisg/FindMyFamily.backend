namespace FindMyFamily.Core.DTOs.Auth;

public record LoginRequestDto(
    string PhoneNumber,
    string Password,
    string? InstallationId,
    string? DeviceModel,
    string? FcmToken
);

public record RefreshTokenRequestDto(
    string RefreshToken,
    string? InstallationId,
    string? DeviceModel,
    string? FcmToken
);

public record LogoutRequestDto(
    string? RefreshToken,
    bool AllDevices = false
);

public record UserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Role,
    DateTime CreatedAt
);

// El token FCM se omite a propósito: es un identificador sensible y el cliente
// ya lo conoce, no hay motivo para que el servidor lo devuelva.
public record DeviceDto(
    Guid Id,
    string? DeviceModel,
    bool IsActive,
    DateTime? LastActiveAt
);

public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTime RefreshTokenExpiry,
    UserDto User,
    DeviceDto Device
);
