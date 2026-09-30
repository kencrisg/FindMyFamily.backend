namespace FindMyFamily.Core.DTOs.Families;

public record CreateFamilyRequestDto(
    string Name
);

public record AddFamilyMemberRequestDto(
    string PhoneNumber,
    string Role = "member"
);

public record UpdateMemberRoleRequestDto(
    string Role
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
