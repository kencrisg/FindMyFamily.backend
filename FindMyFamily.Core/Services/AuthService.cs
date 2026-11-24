using FindMyFamily.Core.Common;
using FindMyFamily.Core.DTOs.Auth;
using FindMyFamily.Core.Entities;
using FindMyFamily.Core.Exceptions;
using FindMyFamily.Core.Interfaces.Repositories;
using FindMyFamily.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IDeviceRepository deviceRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IRefreshTokenHasher refreshTokenHasher,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _deviceRepository = deviceRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _refreshTokenHasher = refreshTokenHasher;
        _logger = logger;
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

        var installationId = string.IsNullOrWhiteSpace(request.InstallationId)
            ? null
            : request.InstallationId.Trim();

        // Antes de crear la sesión se aplican las dos reglas de propiedad del dispositivo:
        // un teléfono no puede estar vinculado a dos cuentas y una cuenta no puede tener
        // dos teléfonos. Los registros que sobran se eliminan aquí mismo.
        var device = await ReconcileDevicesAsync(user.Id, installationId, cancellationToken);

        if (device == null)
        {
            device = new Device
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                InstallationId = installationId,
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
            var devices = await _deviceRepository.GetByUserIdAsync(userId, cancellationToken);
            foreach (var currentDevice in devices)
            {
                _deviceRepository.Delete(currentDevice);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "El usuario {UserId} cerró sesión en todos sus dispositivos. Dispositivos eliminados de la tabla devices: {DeviceCount}.",
                userId, devices.Count);
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

        // Se elimina el registro, no se desactiva: un dispositivo con la sesión cerrada
        // no debe seguir existiendo en la tabla. Antes bastaba desactivarlo, pero el
        // registro (y su token FCM) seguía ahí y el backend podía intentar despertarlo.
        var deviceId = device.Id;
        var installationId = device.InstallationId;
        _deviceRepository.Delete(device);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "El usuario {UserId} cerró sesión en el dispositivo {DeviceId} ({InstallationId}). Se eliminó su registro de la tabla devices.",
            userId, deviceId, installationId ?? "sin InstallationId");
    }

    /// <summary>
    /// Aplica las reglas de propiedad del dispositivo al iniciar sesión:
    /// un teléfono pertenece a una sola cuenta y una cuenta tiene un solo teléfono.
    /// Devuelve el registro que debe reutilizar esta sesión, o null si hay que crear uno.
    /// </summary>
    private async Task<Device?> ReconcileDevicesAsync(
        Guid userId, string? installationId, CancellationToken cancellationToken)
    {
        Device? device = null;

        if (installationId != null)
        {
            var linkedDevices = await _deviceRepository
                .GetByInstallationIdAsync(installationId, cancellationToken);

            device = linkedDevices.FirstOrDefault(d => d.UserId == userId);

            // El teléfono ya estaba vinculado a otra cuenta. Sus credenciales y su token
            // FCM dejan de ser válidos: se elimina ese registro antes de crear el nuevo.
            foreach (var foreignDevice in linkedDevices.Where(d => d.UserId != userId))
            {
                _logger.LogInformation(
                    "El dispositivo {InstallationId} estaba vinculado al usuario {PreviousUserId}. Se elimina ese registro porque ahora inicia sesión el usuario {UserId}.",
                    installationId, foreignDevice.UserId, userId);
                _deviceRepository.Delete(foreignDevice);
            }
        }

        var previousDevices = await _deviceRepository.GetByUserIdAsync(userId, cancellationToken);

        // La cuenta ya tenía otro teléfono: al iniciar sesión en este, el anterior se elimina.
        foreach (var previousDevice in previousDevices.Where(d => d.Id != device?.Id))
        {
            _logger.LogInformation(
                "El usuario {UserId} inició sesión en el dispositivo {NewInstallationId}. Se elimina el registro del dispositivo anterior {PreviousDeviceId} ({PreviousInstallationId}).",
                userId,
                installationId ?? "sin InstallationId",
                previousDevice.Id,
                previousDevice.InstallationId ?? "sin InstallationId");
            _deviceRepository.Delete(previousDevice);
        }

        return device;
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
