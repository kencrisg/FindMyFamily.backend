using FindMyFamily.Core.Common;
using FindMyFamily.Core.DTOs.Users;
using FindMyFamily.Core.Entities;
using FindMyFamily.Core.Exceptions;
using FindMyFamily.Core.Interfaces.Repositories;
using FindMyFamily.Core.Interfaces.Services;

namespace FindMyFamily.Core.Services;

public class UserService : IUserService
{
    private const int MaxPageSize = 100;

    private readonly IUserRepository _userRepository;
    private readonly IFamilyRepository _familyRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(
        IUserRepository userRepository,
        IFamilyRepository familyRepository,
        IDeviceRepository deviceRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _familyRepository = familyRepository;
        _deviceRepository = deviceRepository;
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
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new BadRequestException("La contraseña actual es requerida.", "INVALID_INPUT");
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

        var normalizedNewPhone = PhoneNumber.Normalize(request.NewPhoneNumber);
        if (normalizedNewPhone == user.PhoneNumber)
        {
            return MapToUserProfileDto(user);
        }

        await EnsurePhoneIsFreeAsync(normalizedNewPhone, cancellationToken);

        user.PhoneNumber = normalizedNewPhone;
        user.UpdatedAt = DateTime.UtcNow;

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToUserProfileDto(user);
    }

    public async Task UpdatePasswordAsync(Guid userId, UpdatePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            throw new BadRequestException("La contraseña actual es requerida.", "INVALID_INPUT");
        }

        PasswordPolicy.Validate(request.NewPassword);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Usuario no encontrado.", "USER_NOT_FOUND");
        }

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedException("La contraseña actual es incorrecta.", "INVALID_PASSWORD");
        }

        // Cambiar la contraseña tiene que echar fuera a quien tuviera la sesión robada:
        // antes los refresh tokens sobrevivían al cambio y seguían siendo válidos 90 días.
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _deviceRepository.RevokeAllRefreshTokensAsync(userId, cancellationToken);
        }, cancellationToken);
    }

    public async Task<UserProfileDto> AdminCreateUserAsync(Guid adminUserId, AdminCreateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        await ValidateSystemAdminAsync(adminUserId, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            throw new BadRequestException("El nombre y el apellido son obligatorios.", "INVALID_INPUT");
        }

        PasswordPolicy.Validate(request.Password);
        var normalizedPhone = PhoneNumber.Normalize(request.PhoneNumber);

        if (request.FamilyId == Guid.Empty)
        {
            throw new BadRequestException("El identificador del grupo familiar (familyId) es obligatorio.", "FAMILY_ID_REQUIRED");
        }

        var family = await _familyRepository.GetByIdAsync(request.FamilyId, cancellationToken);
        if (family == null)
        {
            throw new NotFoundException("El grupo familiar especificado no existe.", "FAMILY_NOT_FOUND");
        }

        await EnsurePhoneIsFreeAsync(normalizedPhone, cancellationToken);

        var familyRole = string.Equals(request.FamilyRole, "admin", StringComparison.OrdinalIgnoreCase) ? "admin" : "member";

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = normalizedPhone,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = "user", // Rol normal de sistema en tabla users
            CreatedAt = DateTime.UtcNow
        };

        var familyMember = new FamilyMember
        {
            FamilyId = family.Id,
            UserId = user.Id,
            Role = familyRole,
            JoinedAt = DateTime.UtcNow,
            Family = family,
            User = user
        };

        user.FamilyMembers.Add(familyMember);

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToUserProfileDto(user);
    }

    public async Task<UserProfileDto> AdminGetUserByIdAsync(Guid adminUserId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        await ValidateSystemAdminAsync(adminUserId, cancellationToken);

        var user = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Usuario no encontrado.", "USER_NOT_FOUND");
        }

        return MapToUserProfileDto(user);
    }

    public async Task<UserProfileDto> AdminUpdateUserAsync(Guid adminUserId, Guid targetUserId, AdminUpdateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        await ValidateSystemAdminAsync(adminUserId, cancellationToken);

        var user = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Usuario no encontrado.", "USER_NOT_FOUND");
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName))
        {
            user.FirstName = request.FirstName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.LastName))
        {
            user.LastName = request.LastName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var normalizedPhone = PhoneNumber.Normalize(request.PhoneNumber);
            if (normalizedPhone != user.PhoneNumber)
            {
                await EnsurePhoneIsFreeAsync(normalizedPhone, cancellationToken);
                user.PhoneNumber = normalizedPhone;
            }
        }

        // Si se especificó cambio de familia o de rol de familia
        if (request.FamilyId.HasValue && request.FamilyId.Value != Guid.Empty)
        {
            var targetFamily = await _familyRepository.GetByIdAsync(request.FamilyId.Value, cancellationToken);
            if (targetFamily == null)
            {
                throw new NotFoundException("El grupo familiar especificado no existe.", "FAMILY_NOT_FOUND");
            }

            var existingMember = user.FamilyMembers.FirstOrDefault(fm => fm.FamilyId == request.FamilyId.Value);
            var desiredFamilyRole = !string.IsNullOrWhiteSpace(request.FamilyRole) && request.FamilyRole.Equals("admin", StringComparison.OrdinalIgnoreCase) ? "admin" : "member";

            if (existingMember == null)
            {
                var newMembership = new FamilyMember
                {
                    FamilyId = targetFamily.Id,
                    UserId = user.Id,
                    Role = desiredFamilyRole,
                    JoinedAt = DateTime.UtcNow
                };
                await _familyRepository.AddMemberAsync(newMembership, cancellationToken);
            }
            else
            {
                existingMember.Role = desiredFamilyRole;
            }
        }

        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);
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

        // Borrado lógico, no físico: se conserva la fila para no perder el histórico de
        // ubicaciones ni romper la auditoría de peticiones que le apuntan.
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            user.DeletedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _familyRepository.RemoveAllMembershipsAsync(user.Id, cancellationToken);
            await _deviceRepository.DeactivateAllAsync(user.Id, cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Restaura una cuenta borrada lógicamente. No la devuelve a ninguna familia: el
    /// Administrador de Sistema debe reasignarla explícitamente.
    /// </summary>
    public async Task<UserProfileDto> AdminRestoreUserAsync(Guid adminUserId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        await ValidateSystemAdminAsync(adminUserId, cancellationToken);

        var user = await _userRepository.GetByIdIncludingDeletedAsync(targetUserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Usuario no encontrado.", "USER_NOT_FOUND");
        }

        if (!user.IsDeleted)
        {
            throw new ConflictException("La cuenta no está eliminada.", "USER_NOT_DELETED");
        }

        user.DeletedAt = null;
        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Se relee desde la base de datos en lugar de mapear la entidad que ya teníamos:
        // sus pertenencias se borraron con ExecuteDelete, que no actualiza el grafo en
        // memoria, así que mapearla directamente devolvería familias que ya no existen.
        var restaurado = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);
        return MapToUserProfileDto(restaurado ?? user);
    }

    public async Task<IReadOnlyList<UserProfileDto>> AdminGetDeletedUsersAsync(Guid adminUserId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        await ValidateSystemAdminAsync(adminUserId, cancellationToken);

        var users = await _userRepository.GetDeletedUsersAsync(page, pageSize, cancellationToken);
        return users.Select(MapToUserProfileDto).ToList();
    }

    public async Task<IReadOnlyList<UserProfileDto>> AdminGetAllUsersAsync(Guid adminUserId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        await ValidateSystemAdminAsync(adminUserId, cancellationToken);

        // Acotar la paginación: page=0 generaba un OFFSET negativo (500 de PostgreSQL)
        // y un pageSize arbitrario volcaba la tabla entera de usuarios.
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var users = await _userRepository.GetAllUsersAsync(safePage, safePageSize, cancellationToken);

        return users.Select(MapToUserProfileDto).ToList();
    }

    /// <summary>
    /// El índice único del teléfono es global, así que una cuenta borrada lógicamente sigue
    /// ocupando su número. Se distingue el caso para devolver un código entendible en vez
    /// de dejar que estalle la restricción de base de datos.
    /// </summary>
    private async Task EnsurePhoneIsFreeAsync(string normalizedPhone, CancellationToken cancellationToken)
    {
        var existing = await _userRepository.GetByPhoneNumberIncludingDeletedAsync(normalizedPhone, cancellationToken);
        if (existing == null)
        {
            return;
        }

        if (existing.IsDeleted)
        {
            throw new ConflictException(
                "Ese número perteneció a una cuenta eliminada. Restáurala o usa otro número.",
                "PHONE_BELONGS_TO_DELETED_USER");
        }

        throw new ConflictException("El número de teléfono ya se encuentra registrado.", "PHONE_ALREADY_EXISTS");
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
        var families = user.FamilyMembers.Select(fm => new UserFamilyMembershipDto(
            FamilyId: fm.FamilyId,
            FamilyName: fm.Family?.Name ?? string.Empty,
            FamilyRole: fm.Role,
            JoinedAt: fm.JoinedAt
        )).ToList();

        return new UserProfileDto(
            Id: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            PhoneNumber: user.PhoneNumber,
            SystemRole: user.Role,
            Families: families,
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt
        );
    }
}
