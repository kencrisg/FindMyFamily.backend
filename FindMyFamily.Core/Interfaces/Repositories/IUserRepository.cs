using FindMyFamily.Core.Entities;

namespace FindMyFamily.Core.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<bool> ExistsByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetAllUsersAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    void Update(User user);
    void Delete(User user);
}
