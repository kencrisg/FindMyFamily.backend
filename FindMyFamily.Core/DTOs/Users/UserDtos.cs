namespace FindMyFamily.Core.DTOs.Users;

public record UserFamilyMembershipDto(
    Guid FamilyId,
    string FamilyName,
    string FamilyRole,
    DateTime JoinedAt
);

public record UserProfileDto(
    Guid Id,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string SystemRole,
    IReadOnlyList<UserFamilyMembershipDto> Families,
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
    Guid FamilyId,
    string FamilyRole = "member"
);

public record AdminUpdateUserRequestDto(
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    Guid? FamilyId,
    string? FamilyRole
);
