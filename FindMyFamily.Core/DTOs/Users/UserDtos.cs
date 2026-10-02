namespace FindMyFamily.Core.DTOs.Users;

public record UserProfileDto(
    Guid Id,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Role,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record UpdatePhoneRequestDto(
    string NewPhoneNumber,
    string Password
);

public record UpdatePasswordRequestDto(
    string CurrentPassword,
    string NewPassword
);

public record AdminCreateUserRequestDto(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Password,
    string Role = "user"
);

public record AdminUpdateUserRoleRequestDto(
    string Role
);
