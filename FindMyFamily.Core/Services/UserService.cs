using FindMyFamily.Core.DTOs.Users;
using FindMyFamily.Core.Entities;
using FindMyFamily.Core.Exceptions;
using FindMyFamily.Core.Interfaces.Repositories;
using FindMyFamily.Core.Interfaces.Services;

namespace FindMyFamily.Core.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Usuario no encontrado.", "USER_NOT_FOUND");
        }

        return MapToUserProfileDto(user);
    }

    public async Task<UserProfileDto> UpdatePhoneNumberAsync(Guid userId, UpdatePhoneRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPhoneNumber) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new BadRequestException("El nuevo número de teléfono y la contraseña actual son requeridos.", "INVALID_INPUT");
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Usuario no encontrado.", "USER_NOT_FOUND");
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("La contraseña ingresada es incorrecta.", "INVALID_PASSWORD");
        }

        var normalizedNewPhone = request.NewPhoneNumber.Trim();
        if (normalizedNewPhone == user.PhoneNumber)
        {
            return MapToUserProfileDto(user);
        }

        var exists = await _userRepository.ExistsByPhoneNumberAsync(normalizedNewPhone, cancellationToken);
        if (exists)
        {
            throw new ConflictException("El número de teléfono ya está en uso por otra cuenta.", "PHONE_ALREADY_EXISTS");
        }

        user.PhoneNumber = normalizedNewPhone;
        user.UpdatedAt = DateTime.UtcNow;

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToUserProfileDto(user);
    }

    public async Task UpdatePasswordAsync(Guid userId, UpdatePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new BadRequestException("La contraseña actual y la nueva contraseña son requeridas.", "INVALID_INPUT");
        }

        if (request.NewPassword.Length < 6)
        {
            throw new BadRequestException("La nueva contraseña debe tener al menos 6 caracteres.", "PASSWORD_TOO_SHORT");
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Usuario no encontrado.", "USER_NOT_FOUND");
        }

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedException("La contraseña actual es incorrecta.", "INVALID_PASSWORD");
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserProfileDto> AdminCreateUserAsync(Guid adminUserId, AdminCreateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        await ValidateSystemAdminAsync(adminUserId, cancellationToken);

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

        var role = request.Role.Trim().ToLowerInvariant() == "admin" ? "admin" : "user";

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = normalizedPhone,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = role,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToUserProfileDto(user);
    }

    public async Task AdminDeleteUserAsync(Guid adminUserId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        await ValidateSystemAdminAsync(adminUserId, cancellationToken);

        if (adminUserId == targetUserId)
        {
            throw new BadRequestException("No puedes eliminar tu propia cuenta de administrador.", "CANNOT_DELETE_SELF");
        }

        var user = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Usuario no encontrado.", "USER_NOT_FOUND");
        }

        _userRepository.Delete(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserProfileDto>> AdminGetAllUsersAsync(Guid adminUserId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        await ValidateSystemAdminAsync(adminUserId, cancellationToken);

        var users = await _userRepository.GetAllUsersAsync(page, pageSize, cancellationToken);

        return users.Select(MapToUserProfileDto).ToList();
    }

    private async Task ValidateSystemAdminAsync(Guid adminUserId, CancellationToken cancellationToken)
    {
        var adminUser = await _userRepository.GetByIdAsync(adminUserId, cancellationToken);
        if (adminUser == null || !adminUser.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Se requieren permisos de Administrador de Sistema para realizar esta acción.", "SYSTEM_ADMIN_REQUIRED");
        }
    }

    private static UserProfileDto MapToUserProfileDto(User user)
    {
        return new UserProfileDto(
            Id: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            PhoneNumber: user.PhoneNumber,
            Role: user.Role,
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt
        );
    }
}
