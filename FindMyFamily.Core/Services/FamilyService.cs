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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public FamilyService(
        IFamilyRepository familyRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher)
    {
        _familyRepository = familyRepository;
        _userRepository = userRepository;
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
        var caller = await _userRepository.GetByIdAsync(userId, cancellationToken);
        var isSuperAdmin = caller != null && caller.Role.Equals("admin", StringComparison.OrdinalIgnoreCase);

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
        var caller = await _userRepository.GetByIdAsync(requestingUserId, cancellationToken);
        var isSuperAdmin = caller != null && caller.Role.Equals("admin", StringComparison.OrdinalIgnoreCase);

        var callerMember = await _familyRepository.GetMemberAsync(familyId, requestingUserId, cancellationToken);
        var isFamilyAdmin = callerMember != null && callerMember.Role.Equals("admin", StringComparison.OrdinalIgnoreCase);

        if (!isSuperAdmin && !isFamilyAdmin)
        {
            throw new ForbiddenException("Solo los administradores del grupo familiar pueden crear y añadir miembros.", "ADMIN_REQUIRED");
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            throw new BadRequestException("Todos los campos personales (nombre, apellido, teléfono y contraseña) son requeridos.", "INVALID_INPUT");
        }

        var normalizedPhone = request.PhoneNumber.Trim();
        var exists = await _userRepository.ExistsByPhoneNumberAsync(normalizedPhone, cancellationToken);
        if (exists)
        {
            throw new ConflictException("El número de teléfono ya se encuentra registrado.", "PHONE_ALREADY_EXISTS");
        }

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
            JoinedAt = DateTime.UtcNow,
            Family = family,
            User = newMemberUser
        };

        newMemberUser.FamilyMembers.Add(familyMember);

        await _userRepository.AddAsync(newMemberUser, cancellationToken);
        await _familyRepository.AddMemberAsync(familyMember, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updatedFamily = await _familyRepository.GetByIdWithMembersAsync(familyId, cancellationToken);
        return MapToFamilyDetailDto(updatedFamily!);
    }

    public async Task<FamilyDetailDto> AddMemberByPhoneAsync(Guid requestingUserId, Guid familyId, AddFamilyMemberRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            throw new BadRequestException("El número de teléfono es requerido.", "INVALID_INPUT");
        }

        var caller = await _userRepository.GetByIdAsync(requestingUserId, cancellationToken);
        var isSuperAdmin = caller != null && caller.Role.Equals("admin", StringComparison.OrdinalIgnoreCase);

        var callerMember = await _familyRepository.GetMemberAsync(familyId, requestingUserId, cancellationToken);
        var isFamilyAdmin = callerMember != null && callerMember.Role.Equals("admin", StringComparison.OrdinalIgnoreCase);

        if (!isSuperAdmin && !isFamilyAdmin)
        {
            throw new ForbiddenException("Solo los administradores del grupo familiar pueden añadir miembros.", "ADMIN_REQUIRED");
        }

        var normalizedPhone = request.PhoneNumber.Trim();
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

        var role = string.Equals(request.Role, "admin", StringComparison.OrdinalIgnoreCase) ? "admin" : "member";

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

    public async Task RemoveMemberAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var caller = await _userRepository.GetByIdAsync(requestingUserId, cancellationToken);
        var isSuperAdmin = caller != null && caller.Role.Equals("admin", StringComparison.OrdinalIgnoreCase);

        var callerMember = await _familyRepository.GetMemberAsync(familyId, requestingUserId, cancellationToken);
        var isFamilyAdmin = callerMember != null && callerMember.Role.Equals("admin", StringComparison.OrdinalIgnoreCase);

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

        _familyRepository.RemoveMember(targetMember);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateMemberRoleAsync(Guid requestingUserId, Guid familyId, Guid targetUserId, UpdateMemberRoleRequestDto request, CancellationToken cancellationToken = default)
    {
        var caller = await _userRepository.GetByIdAsync(requestingUserId, cancellationToken);
        var isSuperAdmin = caller != null && caller.Role.Equals("admin", StringComparison.OrdinalIgnoreCase);

        var callerMember = await _familyRepository.GetMemberAsync(familyId, requestingUserId, cancellationToken);
        var isFamilyAdmin = callerMember != null && callerMember.Role.Equals("admin", StringComparison.OrdinalIgnoreCase);

        if (!isSuperAdmin && !isFamilyAdmin)
        {
            throw new ForbiddenException("Solo los administradores pueden modificar roles.", "ADMIN_REQUIRED");
        }

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

        targetMember.Role = normalizedRole;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateSystemAdminAsync(Guid adminUserId, CancellationToken cancellationToken)
    {
        var adminUser = await _userRepository.GetByIdAsync(adminUserId, cancellationToken);
        if (adminUser == null || !adminUser.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Se requieren permisos de Administrador de Sistema para realizar esta acción.", "SYSTEM_ADMIN_REQUIRED");
        }
    }

    private static FamilyDetailDto MapToFamilyDetailDto(Family family)
    {
        var members = family.FamilyMembers.Select(fm => new FamilyMemberSummaryDto(
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
