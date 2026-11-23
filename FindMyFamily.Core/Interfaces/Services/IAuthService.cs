using FindMyFamily.Core.DTOs.Auth;

namespace FindMyFamily.Core.Interfaces.Services;

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca el refresh token del dispositivo actual (o de todos los del usuario).
    /// Sin esto, cerrar sesión en la app dejaba el token válido en servidor durante 90 días.
    /// </summary>
    Task LogoutAsync(Guid userId, LogoutRequestDto request, CancellationToken cancellationToken = default);
}
