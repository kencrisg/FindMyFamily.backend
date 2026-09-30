using FindMyFamily.Core.Entities;
using FindMyFamily.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FindMyFamily.Infrastructure.Persistence.Repositories;

public class FamilyRepository : IFamilyRepository
{
    private readonly ApplicationDbContext _context;

    public FamilyRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Family?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Families
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<Family?> GetByIdWithMembersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Families
            .Include(f => f.FamilyMembers)
                .ThenInclude(fm => fm.User)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Family>> GetFamiliesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Families
            .Where(f => f.FamilyMembers.Any(fm => fm.UserId == userId))
            .Include(f => f.FamilyMembers)
                .ThenInclude(fm => fm.User)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsMemberAsync(Guid familyId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.FamilyMembers
            .AnyAsync(fm => fm.FamilyId == familyId && fm.UserId == userId, cancellationToken);
    }

    public async Task<FamilyMember?> GetMemberAsync(Guid familyId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.FamilyMembers
            .Include(fm => fm.User)
            .Include(fm => fm.Family)
            .FirstOrDefaultAsync(fm => fm.FamilyId == familyId && fm.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(Family family, CancellationToken cancellationToken = default)
    {
        await _context.Families.AddAsync(family, cancellationToken);
    }

    public async Task AddMemberAsync(FamilyMember member, CancellationToken cancellationToken = default)
    {
        await _context.FamilyMembers.AddAsync(member, cancellationToken);
    }

    public void RemoveMember(FamilyMember member)
    {
        _context.FamilyMembers.Remove(member);
    }

    public void Update(Family family)
    {
        _context.Families.Update(family);
    }

    public void Delete(Family family)
    {
        _context.Families.Remove(family);
    }
}
