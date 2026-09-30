using FindMyFamily.Core.Entities;

namespace FindMyFamily.Core.Interfaces.Repositories;

public interface IFamilyRepository
{
    Task<Family?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Family?> GetByIdWithMembersAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Family>> GetFamiliesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsMemberAsync(Guid familyId, Guid userId, CancellationToken cancellationToken = default);
    Task<FamilyMember?> GetMemberAsync(Guid familyId, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Family family, CancellationToken cancellationToken = default);
    Task AddMemberAsync(FamilyMember member, CancellationToken cancellationToken = default);
    void RemoveMember(FamilyMember member);
    void Update(Family family);
    void Delete(Family family);
}
