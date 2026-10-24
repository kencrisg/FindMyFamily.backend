using FindMyFamily.Core.Entities;
using FindMyFamily.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FindMyFamily.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Devices)
            .Include(u => u.FamilyMembers)
                .ThenInclude(fm => fm.Family)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Devices)
            .Include(u => u.FamilyMembers)
                .ThenInclude(fm => fm.Family)
            .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetAllUsersAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Devices)
            .Include(u => u.FamilyMembers)
                .ThenInclude(fm => fm.Family)
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<User?> GetByPhoneNumberIncludingDeletedAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber, cancellationToken);
    }

    public async Task<User?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetDeletedUsersAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        return await _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.DeletedAt != null)
            .OrderByDescending(u => u.DeletedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AnySystemAdminAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Role.ToLower() == "admin", cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }

    /// <summary>
    /// Marca como modificada SOLO la entidad raíz.
    /// `DbSet.Update()` marca todo el grafo cargado, y como las consultas traen los
    /// dispositivos y pertenencias con Include, al guardar reescribía esos valores desde
    /// la memoria. Eso resucitaba un refresh token que se acababa de revocar con
    /// ExecuteUpdate, dejando la sesión viva tras un cambio de contraseña.
    /// Si la entidad ya está rastreada, el seguimiento de cambios se encarga solo.
    /// </summary>
    public void Update(User user)
    {
        var entry = _context.Entry(user);
        if (entry.State == EntityState.Detached)
        {
            entry.State = EntityState.Modified;
        }
    }

    public void Delete(User user)
    {
        _context.Users.Remove(user);
    }
}
