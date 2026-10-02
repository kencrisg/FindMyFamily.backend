using FindMyFamily.Core.DTOs.Users;

namespace FindMyFamily.Core.Interfaces.Services;

public interface IUserService
{
    // Operaciones de Usuario Propio
    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserProfileDto> UpdatePhoneNumberAsync(Guid userId, UpdatePhoneRequestDto request, CancellationToken cancellationToken = default);
    Task UpdatePasswordAsync(Guid userId, UpdatePasswordRequestDto request, CancellationToken cancellationToken = default);

    // Operaciones de Administrador de Sistema
    Task<UserProfileDto> AdminCreateUserAsync(Guid adminUserId, AdminCreateUserRequestDto request, CancellationToken cancellationToken = default);
    Task AdminDeleteUserAsync(Guid adminUserId, Guid targetUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserProfileDto>> AdminGetAllUsersAsync(Guid adminUserId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
}
