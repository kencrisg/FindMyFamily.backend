namespace FindMyFamily.Core.DTOs.Auth;

public record RegisterRequestDto(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Password,
    string? DeviceModel,
    string? FcmToken
);

public record LoginRequestDto(
    string PhoneNumber,
    string Password,
    string? DeviceModel,
    string? FcmToken
);

public record RefreshTokenRequestDto(
    string RefreshToken,
    string? DeviceModel,
    string? FcmToken
);

public record UserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateTime CreatedAt
);

public record DeviceDto(
    Guid Id,
    string? DeviceModel,
    string? FcmToken,
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
