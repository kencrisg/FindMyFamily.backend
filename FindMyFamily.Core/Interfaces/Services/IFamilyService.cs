using FindMyFamily.Core.DTOs.Families;

namespace FindMyFamily.Core.Interfaces.Services;

public interface IFamilyService
{
    // Operaciones de Super Admin
    Task<FamilyDto> CreateFamilyAsync(Guid adminUserId, CreateFamilyRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FamilyDto>> GetAllFamiliesAdminAsync(Guid adminUserId, CancellationToken cancellationToken = default);
    Task<FamilyDto> UpdateFamilyAsync(Guid adminUserId, Guid familyId, UpdateFamilyRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteFamilyAsync(Guid adminUserId, Guid familyId, CancellationToken cancellationToken = default);

    // Operaciones de Miembros y Admin Familiar
    Task<IReadOnlyList<FamilyDto>> GetUserFamiliesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<FamilyDetailDto> GetFamilyByIdAsync(Guid userId, Guid familyId, CancellationToken cancellationToken = default);
    Task<FamilyDetailDto> AddMemberByPhoneAsync(Guid requestingUserId, Guid familyId, AddFamilyMemberRequestDto request, CancellationToken cancellationToken = default);
    Task<FamilyDetailDto> CreateMemberUserAsync(Guid requestingUserId, Guid familyId, CreateFamilyMemberUserRequestDto request, CancellationToken cancellationToken = default);
    Task<FamilyDetailDto> UpdateMemberUserAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, UpdateFamilyMemberUserRequestDto request, CancellationToken cancellationToken = default);
    Task ResetMemberPasswordAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, ResetMemberPasswordRequestDto request, CancellationToken cancellationToken = default);
    Task RemoveMemberAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, CancellationToken cancellationToken = default);
    Task UpdateMemberRoleAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, UpdateMemberRoleRequestDto request, CancellationToken cancellationToken = default);
}
