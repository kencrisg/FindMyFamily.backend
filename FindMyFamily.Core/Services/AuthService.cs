using FindMyFamily.Core.Common;
using FindMyFamily.Core.DTOs.Auth;
using FindMyFamily.Core.Entities;
using FindMyFamily.Core.Exceptions;
using FindMyFamily.Core.Interfaces.Repositories;
using FindMyFamily.Core.Interfaces.Services;

namespace FindMyFamily.Core.Services;

public class AuthService : IAuthService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(90);

    private readonly IUserRepository _userRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IRefreshTokenHasher _refreshTokenHasher;

    public AuthService(
        IUserRepository userRepository,
        IDeviceRepository deviceRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IRefreshTokenHasher refreshTokenHasher)
    {
        _userRepository = userRepository;
        _deviceRepository = deviceRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _refreshTokenHasher = refreshTokenHasher;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new BadRequestException("El número de teléfono y la contraseña son obligatorios.", "INVALID_INPUT");
        }

        var normalizedPhone = PhoneNumber.Normalize(request.PhoneNumber);
        var user = await _userRepository.GetByPhoneNumberAsync(normalizedPhone, cancellationToken);

        // Se verifica el hash incluso cuando el usuario no existe, contra un hash señuelo,
        // para que el tiempo de respuesta no revele qué teléfonos están registrados.
        var passwordOk = user != null
            ? _passwordHasher.VerifyPassword(request.Password, user.PasswordHash)
            : _passwordHasher.VerifyDummy(request.Password);

        if (user == null || !passwordOk)
        {
            throw new UnauthorizedException("Credenciales incorrectas. Verifique su teléfono o contraseña.", "INVALID_CREDENTIALS");
        }

        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.Add(RefreshTokenLifetime);

        // El dispositivo se identifica por su InstallationId, no por su modelo: dos teléfonos
        // del mismo modelo son dos dispositivos distintos y deben mantener sesiones separadas.
        Device? device = null;
        if (!string.IsNullOrWhiteSpace(request.InstallationId))
        {
            device = await _deviceRepository.GetByInstallationIdAsync(
                user.Id, request.InstallationId.Trim(), cancellationToken);
        }

        if (device == null)
        {
            device = new Device
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                InstallationId = request.InstallationId?.Trim(),
                DeviceModel = request.DeviceModel,
                FcmToken = request.FcmToken,
                RefreshTokenHash = _refreshTokenHasher.Hash(refreshToken),
                RefreshTokenExpiry = refreshTokenExpiry,
                LastActiveAt = DateTime.UtcNow,
                IsActive = true
            };
            await _deviceRepository.AddAsync(device, cancellationToken);
        }
        else
        {
            device.RefreshTokenHash = _refreshTokenHasher.Hash(refreshToken);
            device.RefreshTokenExpiry = refreshTokenExpiry;
            device.LastActiveAt = DateTime.UtcNow;
            device.IsActive = true;

            if (!string.IsNullOrWhiteSpace(request.DeviceModel))
            {
                device.DeviceModel = request.DeviceModel;
            }

            if (!string.IsNullOrWhiteSpace(request.FcmToken))
            {
                device.FcmToken = request.FcmToken;
            }

            _deviceRepository.Update(device);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user, device);

        return MapToAuthResponse(user, device, accessToken, refreshToken, refreshTokenExpiry);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new BadRequestException("El refresh token es obligatorio.", "INVALID_REFRESH_TOKEN");
        }

        var presentedHash = _refreshTokenHasher.Hash(request.RefreshToken);
        var device = await _deviceRepository.GetByRefreshTokenHashAsync(presentedHash, cancellationToken);

        if (device == null || !device.IsActive)
        {
            throw new UnauthorizedException("Refresh token inválido.", "INVALID_REFRESH_TOKEN");
        }

        if (device.RefreshTokenExpiry == null || device.RefreshTokenExpiry < DateTime.UtcNow)
        {
            throw new UnauthorizedException("El refresh token ha expirado. Inicia sesión nuevamente.", "EXPIRED_REFRESH_TOKEN");
        }

        var newRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        var newRefreshTokenExpiry = DateTime.UtcNow.Add(RefreshTokenLifetime);

        device.RefreshTokenHash = _refreshTokenHasher.Hash(newRefreshToken);
        device.RefreshTokenExpiry = newRefreshTokenExpiry;
        device.LastActiveAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(request.FcmToken))
        {
            device.FcmToken = request.FcmToken;
        }

        if (!string.IsNullOrEmpty(request.DeviceModel))
        {
            device.DeviceModel = request.DeviceModel;
        }

        if (string.IsNullOrWhiteSpace(device.InstallationId) && !string.IsNullOrWhiteSpace(request.InstallationId))
        {
            device.InstallationId = request.InstallationId.Trim();
        }

        _deviceRepository.Update(device);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // El filtro global de usuarios borrados deja esta navegación en null cuando la cuenta
        // se eliminó lógicamente. No debería ocurrir (el borrado revoca los tokens), pero sin
        // esta guarda sería una NullReferenceException y un 500 en lugar de un 401 limpio.
        var user = device.User;
        if (user == null || user.IsDeleted)
        {
            throw new UnauthorizedException("La cuenta ya no está activa.", "ACCOUNT_DISABLED");
        }

        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user, device);

        return MapToAuthResponse(user, device, accessToken, newRefreshToken, newRefreshTokenExpiry);
    }

    public async Task LogoutAsync(Guid userId, LogoutRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.AllDevices)
        {
            await _deviceRepository.DeactivateAllAsync(userId, cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new BadRequestException(
                "Indica el refresh token a revocar o solicita el cierre en todos los dispositivos.",
                "REFRESH_TOKEN_REQUIRED");
        }

        var presentedHash = _refreshTokenHasher.Hash(request.RefreshToken);
        var device = await _deviceRepository.GetByRefreshTokenHashAsync(presentedHash, cancellationToken);

        // El token debe pertenecer a quien pide el cierre: si no, no se revela si existe.
        if (device == null || device.UserId != userId)
        {
            throw new UnauthorizedException("Refresh token inválido.", "INVALID_REFRESH_TOKEN");
        }

        RevokeDevice(device);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }


    /// <summary>
    /// Deja el dispositivo fuera de servicio al cerrar sesión.
    ///
    /// No basta con revocar el refresh token: mientras el dispositivo conserve su token FCM
    /// y siga activo, el backend lo considera alcanzable. Un familiar pedía su ubicación, el
    /// backend respondía REQUESTED y le enviaba el Silent Push, de modo que un teléfono con
    /// la sesión cerrada **encendía el GPS** para luego no poder reportar nada (sin sesión,
    /// el POST da 401). Se gastaba batería, se invadía la privacidad de quien ya había
    /// salido, y el solicitante esperaba 30 segundos en vez de ver "no disponible".
    /// </summary>
    private void RevokeDevice(Device device)
    {
        device.RefreshTokenHash = null;
        device.RefreshTokenExpiry = null;
        device.FcmToken = null;
        device.IsActive = false;
        _deviceRepository.Update(device);
    }

    private static AuthResponseDto MapToAuthResponse(User user, Device device, string accessToken, string refreshToken, DateTime expiry)
    {
        var userDto = new UserDto(user.Id, user.FirstName, user.LastName, user.PhoneNumber, user.Role, user.CreatedAt);
        var deviceDto = new DeviceDto(device.Id, device.DeviceModel, device.IsActive, device.LastActiveAt);

        return new AuthResponseDto(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            RefreshTokenExpiry: expiry,
            User: userDto,
            Device: deviceDto
        );
    }
}
