using FindMyFamily.Core.DTOs.Auth;
using FindMyFamily.Core.Entities;
using FindMyFamily.Core.Exceptions;
using FindMyFamily.Core.Interfaces.Repositories;
using FindMyFamily.Core.Interfaces.Services;

namespace FindMyFamily.Core.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(
        IUserRepository userRepository,
        IDeviceRepository deviceRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository;
        _deviceRepository = deviceRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new BadRequestException("El número de teléfono y la contraseña son obligatorios.", "INVALID_INPUT");
        }

        var normalizedPhone = request.PhoneNumber.Trim();
        var exists = await _userRepository.ExistsByPhoneNumberAsync(normalizedPhone, cancellationToken);
        if (exists)
        {
            throw new ConflictException("El número de teléfono ya se encuentra registrado.", "PHONE_ALREADY_EXISTS");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = normalizedPhone,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(90);

        var device = new Device
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceModel = request.DeviceModel,
            FcmToken = request.FcmToken,
            RefreshToken = refreshToken,
            RefreshTokenExpiry = refreshTokenExpiry,
            LastActiveAt = DateTime.UtcNow,
            IsActive = true
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _deviceRepository.AddAsync(device, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user, device);

        return MapToAuthResponse(user, device, accessToken, refreshToken, refreshTokenExpiry);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new BadRequestException("El número de teléfono y la contraseña son obligatorios.", "INVALID_INPUT");
        }

        var normalizedPhone = request.PhoneNumber.Trim();
        var user = await _userRepository.GetByPhoneNumberAsync(normalizedPhone, cancellationToken);
        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Credenciales incorrectas. Verifique su teléfono o contraseña.", "INVALID_CREDENTIALS");
        }

        // Buscar dispositivo existente o crear uno nuevo para este login
        var device = user.Devices.FirstOrDefault(d => d.IsActive && d.DeviceModel == request.DeviceModel);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(90);

        if (device == null)
        {
            device = new Device
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                DeviceModel = request.DeviceModel,
                FcmToken = request.FcmToken,
                RefreshToken = refreshToken,
                RefreshTokenExpiry = refreshTokenExpiry,
                LastActiveAt = DateTime.UtcNow,
                IsActive = true
            };
            await _deviceRepository.AddAsync(device, cancellationToken);
        }
        else
        {
            device.RefreshToken = refreshToken;
            device.RefreshTokenExpiry = refreshTokenExpiry;
            device.LastActiveAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(request.FcmToken))
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

        var device = await _deviceRepository.GetByRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (device == null || !device.IsActive)
        {
            throw new UnauthorizedException("Refresh token inválido.", "INVALID_REFRESH_TOKEN");
        }

        if (device.RefreshTokenExpiry == null || device.RefreshTokenExpiry < DateTime.UtcNow)
        {
            throw new UnauthorizedException("El refresh token ha expirado. Inicia sesión nuevamente.", "EXPIRED_REFRESH_TOKEN");
        }

        var newRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        var newRefreshTokenExpiry = DateTime.UtcNow.AddDays(90);

        device.RefreshToken = newRefreshToken;
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

        _deviceRepository.Update(device);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var user = device.User;
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user, device);

        return MapToAuthResponse(user, device, accessToken, newRefreshToken, newRefreshTokenExpiry);
    }

    private static AuthResponseDto MapToAuthResponse(User user, Device device, string accessToken, string refreshToken, DateTime expiry)
    {
        var userDto = new UserDto(user.Id, user.FirstName, user.LastName, user.PhoneNumber, user.Role, user.CreatedAt);
        var deviceDto = new DeviceDto(device.Id, device.DeviceModel, device.FcmToken, device.IsActive, device.LastActiveAt);

        return new AuthResponseDto(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            RefreshTokenExpiry: expiry,
            User: userDto,
            Device: deviceDto
        );
    }
}
