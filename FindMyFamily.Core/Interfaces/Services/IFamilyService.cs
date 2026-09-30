using FindMyFamily.Core.DTOs.Families;

namespace FindMyFamily.Core.Interfaces.Services;

public interface IFamilyService
{
    Task<FamilyDto> CreateFamilyAsync(Guid userId, CreateFamilyRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FamilyDto>> GetUserFamiliesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<FamilyDetailDto> GetFamilyByIdAsync(Guid userId, Guid familyId, CancellationToken cancellationToken = default);
    Task<FamilyDetailDto> AddMemberByPhoneAsync(Guid requestingUserId, Guid familyId, AddFamilyMemberRequestDto request, CancellationToken cancellationToken = default);
    Task RemoveMemberAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, CancellationToken cancellationToken = default);
    Task UpdateMemberRoleAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, UpdateMemberRoleRequestDto request, CancellationToken cancellationToken = default);
}
