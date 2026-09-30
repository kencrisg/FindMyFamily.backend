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

    public FamilyService(
        IFamilyRepository familyRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _familyRepository = familyRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<FamilyDto> CreateFamilyAsync(Guid userId, CreateFamilyRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException("El nombre del grupo familiar es obligatorio.", "INVALID_INPUT");
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Usuario no encontrado.", "USER_NOT_FOUND");
        }

        var family = new Family
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var member = new FamilyMember
        {
            FamilyId = family.Id,
            UserId = userId,
            Role = "admin",
            JoinedAt = DateTime.UtcNow
        };

        await _familyRepository.AddAsync(family, cancellationToken);
        await _familyRepository.AddMemberAsync(member, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new FamilyDto(
            Id: family.Id,
            Name: family.Name,
            CreatedAt: family.CreatedAt,
            MembersCount: 1
        );
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
        var isMember = await _familyRepository.IsMemberAsync(familyId, userId, cancellationToken);
        if (!isMember)
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

    public async Task<FamilyDetailDto> AddMemberByPhoneAsync(Guid requestingUserId, Guid familyId, AddFamilyMemberRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            throw new BadRequestException("El número de teléfono es requerido.", "INVALID_INPUT");
        }

        var callerMember = await _familyRepository.GetMemberAsync(familyId, requestingUserId, cancellationToken);
        if (callerMember == null || !callerMember.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
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
        var callerMember = await _familyRepository.GetMemberAsync(familyId, requestingUserId, cancellationToken);
        if (callerMember == null)
        {
            throw new ForbiddenException("No perteneces a este grupo familiar.", "FORBIDDEN");
        }

        var isSelfRemoval = requestingUserId == targetUserId;
        if (!isSelfRemoval && !callerMember.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
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
        var callerMember = await _familyRepository.GetMemberAsync(familyId, requestingUserId, cancellationToken);
        if (callerMember == null || !callerMember.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
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
