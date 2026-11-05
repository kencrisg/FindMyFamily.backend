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
    DateTime? UpdatedAt,
    bool LocationSharingEnabled
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

/// <summary>Datos personales que el propio usuario puede cambiar sin intervención de un admin.</summary>
public record UpdateMyProfileRequestDto(
    string FirstName,
    string LastName
);

/// <summary>Restablecimiento de contraseña por el Administrador de Sistema.</summary>
public record AdminResetPasswordRequestDto(
    string NewPassword
);

/// <summary>Activa o desactiva que la familia pueda ver la ubicación del usuario.</summary>
public record UpdateLocationSharingRequestDto(
    bool Enabled
);
