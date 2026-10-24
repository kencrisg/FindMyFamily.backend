using FindMyFamily.Core.Common;
using FindMyFamily.Core.DTOs.Families;
using FindMyFamily.Core.Entities;
using FindMyFamily.Core.Exceptions;
using FindMyFamily.Core.Interfaces.Repositories;
using FindMyFamily.Core.Interfaces.Services;

namespace FindMyFamily.Core.Services;

public class FamilyService : IFamilyService
{
    private readonly IFamilyRepository _familyRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public FamilyService(
        IFamilyRepository familyRepository,
        IUserRepository userRepository,
        IDeviceRepository deviceRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher)
    {
        _familyRepository = familyRepository;
        _userRepository = userRepository;
        _deviceRepository = deviceRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<FamilyDto> CreateFamilyAsync(Guid adminUserId, CreateFamilyRequestDto request, CancellationToken cancellationToken = default)
    {
        await ValidateSystemAdminAsync(adminUserId, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException("El nombre del grupo familiar es obligatorio.", "INVALID_INPUT");
        }

        var family = new Family
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _familyRepository.AddAsync(family, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new FamilyDto(
            Id: family.Id,
            Name: family.Name,
            CreatedAt: family.CreatedAt,
            MembersCount: 0
        );
    }

    public async Task<IReadOnlyList<FamilyDto>> GetAllFamiliesAdminAsync(Guid adminUserId, CancellationToken cancellationToken = default)
    {
        await ValidateSystemAdminAsync(adminUserId, cancellationToken);

        var families = await _familyRepository.GetAllFamiliesAsync(cancellationToken);

        return families.Select(f => new FamilyDto(
            Id: f.Id,
            Name: f.Name,
            CreatedAt: f.CreatedAt,
            MembersCount: f.FamilyMembers.Count
        )).ToList();
    }

    public async Task<FamilyDto> UpdateFamilyAsync(Guid adminUserId, Guid familyId, UpdateFamilyRequestDto request, CancellationToken cancellationToken = default)
    {
        await ValidateSystemAdminAsync(adminUserId, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException("El nuevo nombre de la familia es obligatorio.", "INVALID_INPUT");
        }

        var family = await _familyRepository.GetByIdAsync(familyId, cancellationToken);
        if (family == null)
        {
            throw new NotFoundException("Grupo familiar no encontrado.", "FAMILY_NOT_FOUND");
        }

        family.Name = request.Name.Trim();
        _familyRepository.Update(family);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var members = await _familyRepository.GetByIdWithMembersAsync(familyId, cancellationToken);
        return new FamilyDto(
            Id: family.Id,
            Name: family.Name,
            CreatedAt: family.CreatedAt,
            MembersCount: members?.FamilyMembers.Count ?? 0
        );
    }

    public async Task DeleteFamilyAsync(Guid adminUserId, Guid familyId, CancellationToken cancellationToken = default)
    {
        await ValidateSystemAdminAsync(adminUserId, cancellationToken);

        var family = await _familyRepository.GetByIdAsync(familyId, cancellationToken);
        if (family == null)
        {
            throw new NotFoundException("Grupo familiar no encontrado.", "FAMILY_NOT_FOUND");
        }

        _familyRepository.Delete(family);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FamilyDto>> GetUserFamiliesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var families = await _familyRepository.GetFamiliesByUserIdAsync(userId, cancellationToken);

        return families.Select(f => new FamilyDto(
            Id: f.Id,
            Name: f.Name,
            CreatedAt: f.CreatedAt,
            MembersCount: f.FamilyMembers.Count
        )).ToList();
    }

    public async Task<FamilyDetailDto> GetFamilyByIdAsync(Guid userId, Guid familyId, CancellationToken cancellationToken = default)
    {
        var isSuperAdmin = await IsSystemAdminAsync(userId, cancellationToken);

        var isMember = await _familyRepository.IsMemberAsync(familyId, userId, cancellationToken);
        if (!isMember && !isSuperAdmin)
        {
            throw new ForbiddenException("No tienes permiso para ver este grupo familiar.", "FORBIDDEN");
        }

        var family = await _familyRepository.GetByIdWithMembersAsync(familyId, cancellationToken);
        if (family == null)
        {
            throw new NotFoundException("Grupo familiar no encontrado.", "FAMILY_NOT_FOUND");
        }

        return MapToFamilyDetailDto(family);
    }

    public async Task<FamilyDetailDto> CreateMemberUserAsync(Guid requestingUserId, Guid familyId, CreateFamilyMemberUserRequestDto request, CancellationToken cancellationToken = default)
    {
        await ValidateFamilyAdminAsync(requestingUserId, familyId, cancellationToken,
            "Solo los administradores del grupo familiar pueden crear y añadir miembros.");

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            throw new BadRequestException("El nombre y el apellido son requeridos.", "INVALID_INPUT");
        }

        PasswordPolicy.Validate(request.Password);
        var normalizedPhone = PhoneNumber.Normalize(request.PhoneNumber);

        await EnsurePhoneIsFreeAsync(normalizedPhone, cancellationToken);

        var family = await _familyRepository.GetByIdAsync(familyId, cancellationToken);
        if (family == null)
        {
            throw new NotFoundException("Grupo familiar no encontrado.", "FAMILY_NOT_FOUND");
        }

        var newMemberUser = new User
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = normalizedPhone,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = "user",
            CreatedAt = DateTime.UtcNow
        };

        var familyMember = new FamilyMember
        {
            FamilyId = familyId,
            UserId = newMemberUser.Id,
            Role = "member",
            JoinedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(newMemberUser, cancellationToken);
        await _familyRepository.AddMemberAsync(familyMember, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updatedFamily = await _familyRepository.GetByIdWithMembersAsync(familyId, cancellationToken);
        return MapToFamilyDetailDto(updatedFamily!);
    }

    /// <summary>
    /// Vincula a un usuario YA EXISTENTE a la familia a partir de su teléfono.
    /// Restringido al superadministrador de la plataforma: cuando cualquier admin familiar
    /// podía hacerlo, bastaba conocer (o adivinar) un número para meter a su dueño en la
    /// familia y, acto seguido, pedir su ubicación y leer su historial sin que se enterara,
    /// porque el push es silencioso. La corrección definitiva es un flujo de invitación con
    /// aceptación explícita del destinatario; mientras no exista, esta capacidad no puede
    /// estar en manos de cualquier usuario.
    /// </summary>
    public async Task<FamilyDetailDto> AddMemberByPhoneAsync(Guid requestingUserId, Guid familyId, AddFamilyMemberRequestDto request, CancellationToken cancellationToken = default)
    {
        await ValidateSystemAdminAsync(requestingUserId, cancellationToken);

        var normalizedPhone = PhoneNumber.Normalize(request.PhoneNumber);

        var family = await _familyRepository.GetByIdAsync(familyId, cancellationToken);
        if (family == null)
        {
            throw new NotFoundException("Grupo familiar no encontrado.", "FAMILY_NOT_FOUND");
        }

        var targetUser = await _userRepository.GetByPhoneNumberAsync(normalizedPhone, cancellationToken);
        if (targetUser == null)
        {
            throw new NotFoundException("No se encontró ningún usuario registrado con ese número de teléfono.", "USER_NOT_FOUND");
        }

        var alreadyMember = await _familyRepository.IsMemberAsync(familyId, targetUser.Id, cancellationToken);
        if (alreadyMember)
        {
            throw new ConflictException("El usuario ya es miembro de este grupo familiar.", "ALREADY_MEMBER");
        }

        var role = NormalizeFamilyRole(request.Role);

        var newMember = new FamilyMember
        {
            FamilyId = familyId,
            UserId = targetUser.Id,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };

        await _familyRepository.AddMemberAsync(newMember, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updatedFamily = await _familyRepository.GetByIdWithMembersAsync(familyId, cancellationToken);
        return MapToFamilyDetailDto(updatedFamily!);
    }

    /// <summary>
    /// Edita los datos personales de un miembro de la familia (admin familiar o superadmin).
    /// </summary>
    public async Task<FamilyDetailDto> UpdateMemberUserAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, UpdateFamilyMemberUserRequestDto request, CancellationToken cancellationToken = default)
    {
        var isSuperAdmin = await ValidateFamilyAdminAsync(requestingUserId, familyId, cancellationToken,
            "Solo los administradores del grupo familiar pueden editar a sus miembros.");

        var targetUser = await GetManageableMemberAsync(familyId, targetUserId, isSuperAdmin, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.FirstName))
        {
            targetUser.FirstName = request.FirstName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.LastName))
        {
            targetUser.LastName = request.LastName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var normalizedPhone = PhoneNumber.Normalize(request.PhoneNumber);
            if (normalizedPhone != targetUser.PhoneNumber)
            {
                await EnsurePhoneIsFreeAsync(normalizedPhone, cancellationToken);
                targetUser.PhoneNumber = normalizedPhone;
            }
        }

        targetUser.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(targetUser);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updatedFamily = await _familyRepository.GetByIdWithMembersAsync(familyId, cancellationToken);
        return MapToFamilyDetailDto(updatedFamily!);
    }

    /// <summary>
    /// Restablece la contraseña de un miembro (admin familiar o superadmin). No exige la
    /// contraseña anterior, así que invalida todas las sesiones abiertas de ese miembro:
    /// de lo contrario, quien tuviera su sesión seguiría dentro tras el restablecimiento.
    /// </summary>
    public async Task ResetMemberPasswordAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, ResetMemberPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var isSuperAdmin = await ValidateFamilyAdminAsync(requestingUserId, familyId, cancellationToken,
            "Solo los administradores del grupo familiar pueden restablecer la contraseña de sus miembros.");

        PasswordPolicy.Validate(request.NewPassword);

        var targetUser = await GetManageableMemberAsync(familyId, targetUserId, isSuperAdmin, cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            targetUser.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
            targetUser.UpdatedAt = DateTime.UtcNow;
            _userRepository.Update(targetUser);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Después de guardar: la revocación va directa a la base de datos y no debe
            // quedar expuesta a que un SaveChanges posterior reescriba valores en memoria.
            await _deviceRepository.RevokeAllRefreshTokensAsync(targetUserId, cancellationToken);
        }, cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var isSuperAdmin = await IsSystemAdminAsync(requestingUserId, cancellationToken);
        var callerMember = await _familyRepository.GetMemberAsync(familyId, requestingUserId, cancellationToken);
        var isFamilyAdmin = IsAdminRole(callerMember?.Role);
        var isSelfRemoval = requestingUserId == targetUserId;

        if (!isSelfRemoval && !isSuperAdmin && !isFamilyAdmin)
        {
            throw new ForbiddenException("Solo los administradores pueden eliminar a otros miembros de la familia.", "ADMIN_REQUIRED");
        }

        var targetMember = await _familyRepository.GetMemberAsync(familyId, targetUserId, cancellationToken);
        if (targetMember == null)
        {
            throw new NotFoundException("El usuario no es miembro de este grupo familiar.", "MEMBER_NOT_FOUND");
        }

        // Una familia sin administrador queda huérfana: nadie puede volver a gestionar
        // miembros ni roles sin intervención del superadministrador.
        if (IsAdminRole(targetMember.Role) && !isSuperAdmin)
        {
            var adminCount = await _familyRepository.CountAdminsAsync(familyId, cancellationToken);
            if (adminCount <= 1)
            {
                throw new ConflictException(
                    "No puedes eliminar al único administrador del grupo familiar. Asigna otro administrador primero.",
                    "LAST_FAMILY_ADMIN");
            }
        }

        var targetUser = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);

        // Un admin familiar no puede tocar la cuenta de un Administrador de Sistema.
        if (!isSuperAdmin && !isSelfRemoval && targetUser != null && IsAdminRole(targetUser.Role))
        {
            throw new ForbiddenException(
                "No puedes gestionar la cuenta de un Administrador de Sistema.", "CANNOT_MANAGE_SYSTEM_ADMIN");
        }

        // Si el usuario pertenece a más familias, solo se le saca de esta. Si esta era su
        // única familia, su cuenta se borra lógicamente: limitarse a desvincularlo dejaba
        // una cuenta huérfana que seguía pudiendo iniciar sesión en la app.
        var otherMemberships = targetUser?.FamilyMembers.Count(fm => fm.FamilyId != familyId) ?? 0;
        var debeBorrarCuenta = targetUser != null
            && !isSelfRemoval
            && !IsAdminRole(targetUser.Role)
            && otherMemberships == 0;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _familyRepository.RemoveMember(targetMember);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (debeBorrarCuenta)
            {
                await SoftDeleteAccountAsync(targetUser!, cancellationToken);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Borrado lógico de una cuenta: se marca como eliminada, se la saca de todas sus
    /// familias y se desactivan sus dispositivos revocando sus sesiones. La fila se conserva
    /// para no perder el histórico de ubicaciones ni romper la auditoría de peticiones.
    /// </summary>
    private async Task SoftDeleteAccountAsync(Entities.User user, CancellationToken cancellationToken)
    {
        user.DeletedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _familyRepository.RemoveAllMembershipsAsync(user.Id, cancellationToken);
        await _deviceRepository.DeactivateAllAsync(user.Id, cancellationToken);
    }

    /// <summary>
    /// Resuelve el usuario objetivo comprobando que sea miembro de la familia indicada y
    /// que el llamante tenga derecho a gestionarlo. Un admin familiar no puede editar ni
    /// restablecer la contraseña de un Administrador de Sistema que esté en su familia:
    /// sería una vía de escalada de privilegios.
    /// </summary>
    private async Task<Entities.User> GetManageableMemberAsync(Guid familyId, Guid targetUserId, bool isSuperAdmin, CancellationToken cancellationToken)
    {
        var targetMember = await _familyRepository.GetMemberAsync(familyId, targetUserId, cancellationToken);
        if (targetMember == null)
        {
            throw new NotFoundException("El usuario no es miembro de este grupo familiar.", "MEMBER_NOT_FOUND");
        }

        var targetUser = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (targetUser == null)
        {
            throw new NotFoundException("Usuario no encontrado.", "USER_NOT_FOUND");
        }

        if (!isSuperAdmin && IsAdminRole(targetUser.Role))
        {
            throw new ForbiddenException(
                "No puedes gestionar la cuenta de un Administrador de Sistema.", "CANNOT_MANAGE_SYSTEM_ADMIN");
        }

        return targetUser;
    }

    public async Task UpdateMemberRoleAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, UpdateMemberRoleRequestDto request, CancellationToken cancellationToken = default)
    {
        var isSuperAdmin = await ValidateFamilyAdminAsync(requestingUserId, familyId, cancellationToken,
            "Solo los administradores pueden modificar roles.");

        var normalizedRole = request.Role?.Trim().ToLowerInvariant();
        if (normalizedRole != "admin" && normalizedRole != "member")
        {
            throw new BadRequestException("El rol debe ser 'admin' o 'member'.", "INVALID_ROLE");
        }

        var targetMember = await _familyRepository.GetMemberAsync(familyId, targetUserId, cancellationToken);
        if (targetMember == null)
        {
            throw new NotFoundException("El usuario no es miembro de este grupo familiar.", "MEMBER_NOT_FOUND");
        }

        // Degradar al último administrador deja la familia sin gestión posible.
        if (IsAdminRole(targetMember.Role) && normalizedRole == "member" && !isSuperAdmin)
        {
            var adminCount = await _familyRepository.CountAdminsAsync(familyId, cancellationToken);
            if (adminCount <= 1)
            {
                throw new ConflictException(
                    "No puedes degradar al único administrador del grupo familiar. Asigna otro administrador primero.",
                    "LAST_FAMILY_ADMIN");
            }
        }

        targetMember.Role = normalizedRole;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeFamilyRole(string? role) =>
        string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) ? "admin" : "member";

    private static bool IsAdminRole(string? role) =>
        role != null && role.Equals("admin", StringComparison.OrdinalIgnoreCase);

    private async Task<bool> IsSystemAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user != null && IsAdminRole(user.Role);
    }

    private async Task ValidateSystemAdminAsync(Guid adminUserId, CancellationToken cancellationToken)
    {
        if (!await IsSystemAdminAsync(adminUserId, cancellationToken))
        {
            throw new ForbiddenException("Se requieren permisos de Administrador de Sistema para realizar esta acción.", "SYSTEM_ADMIN_REQUIRED");
        }
    }

    /// <summary>Exige rol de admin familiar o superadmin. Devuelve true si es superadmin.</summary>
    private async Task<bool> ValidateFamilyAdminAsync(Guid requestingUserId, Guid familyId, CancellationToken cancellationToken, string forbiddenMessage)
    {
        var isSuperAdmin = await IsSystemAdminAsync(requestingUserId, cancellationToken);
        if (isSuperAdmin)
        {
            return true;
        }

        var callerMember = await _familyRepository.GetMemberAsync(familyId, requestingUserId, cancellationToken);
        if (!IsAdminRole(callerMember?.Role))
        {
            throw new ForbiddenException(forbiddenMessage, "ADMIN_REQUIRED");
        }

        return false;
    }

    /// <summary>
    /// El índice único del teléfono es global, así que una cuenta borrada lógicamente sigue
    /// ocupando su número. Se distingue el caso para devolver un código entendible en vez
    /// de dejar que estalle la restricción de base de datos con un 500.
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
                "Ese número perteneció a una cuenta eliminada. Pide al Administrador de Sistema que la restaure o usa otro número.",
                "PHONE_BELONGS_TO_DELETED_USER");
        }

        throw new ConflictException("El número de teléfono ya se encuentra registrado.", "PHONE_ALREADY_EXISTS");
    }

    private static FamilyDetailDto MapToFamilyDetailDto(Family family)
    {
        var members = family.FamilyMembers
            // Defensivo: el filtro global oculta los usuarios borrados, así que una
            // pertenencia residual llegaría con User en null y produciría una fila vacía.
            .Where(fm => fm.User != null)
            .Select(fm => new FamilyMemberSummaryDto(
            UserId: fm.UserId,
            FirstName: fm.User?.FirstName ?? string.Empty,
            LastName: fm.User?.LastName ?? string.Empty,
            PhoneNumber: fm.User?.PhoneNumber ?? string.Empty,
            Role: fm.Role,
            JoinedAt: fm.JoinedAt
        )).ToList();

        return new FamilyDetailDto(
            Id: family.Id,
            Name: family.Name,
            CreatedAt: family.CreatedAt,
            Members: members
        );
    }
}
