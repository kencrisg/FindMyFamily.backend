namespace FindMyFamily.Core.DTOs.Families;

public record CreateFamilyRequestDto(
    string Name
);

public record UpdateFamilyRequestDto(
    string Name
);

public record AddFamilyMemberRequestDto(
    string PhoneNumber,
    string Role = "member"
);

public record CreateFamilyMemberUserRequestDto(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Password
);

public record UpdateMemberRoleRequestDto(
    string Role
);

/// <summary>Edición de los datos de un miembro por parte del admin de la familia.</summary>
public record UpdateFamilyMemberUserRequestDto(
    string? FirstName,
    string? LastName,
    string? PhoneNumber
);

/// <summary>
/// Restablecimiento de la contraseña de un miembro por parte del admin de la familia
/// (caso típico: un padre que gestiona la cuenta de un hijo que la ha olvidado).
/// No pide la contraseña anterior, así que revoca todas las sesiones del miembro.
/// </summary>
public record ResetMemberPasswordRequestDto(
    string NewPassword
);

public record FamilyMemberSummaryDto(
    Guid UserId,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Role,
    DateTime JoinedAt
);

public record FamilyDto(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    int MembersCount
);

public record FamilyDetailDto(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    IReadOnlyList<FamilyMemberSummaryDto> Members
);
