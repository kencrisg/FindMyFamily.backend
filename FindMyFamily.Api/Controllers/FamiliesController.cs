using System.Security.Claims;
using FindMyFamily.Core.DTOs.Families;
using FindMyFamily.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FindMyFamily.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FamiliesController : ControllerBase
{
    private readonly IFamilyService _familyService;

    public FamiliesController(IFamilyService familyService)
    {
        _familyService = familyService;
    }

    private Guid CurrentUserId
    {
        get
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            {
                throw new UnauthorizedAccessException("Usuario no autenticado.");
            }
            return userId;
        }
    }

    private bool IsSuperAdmin => User.IsInRole("admin");

    /// <summary>
    /// Crea un nuevo grupo familiar en el sistema (Exclusivo para Super Admin).
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpPost]
    [ProducesResponseType(typeof(FamilyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateFamily([FromBody] CreateFamilyRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _familyService.CreateFamilyAsync(CurrentUserId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Lista los grupos familiares. Para Super Admin devuelve todas las familias del sistema; para usuarios normales solo a las que pertenecen.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFamilies(CancellationToken cancellationToken)
    {
        if (IsSuperAdmin)
        {
            var allFamilies = await _familyService.GetAllFamiliesAdminAsync(CurrentUserId, cancellationToken);
            return Ok(allFamilies);
        }

        var userFamilies = await _familyService.GetUserFamiliesAsync(CurrentUserId, cancellationToken);
        return Ok(userFamilies);
    }

    /// <summary>
    /// Obtiene el detalle de un grupo familiar y todos sus miembros.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FamilyDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFamilyById(Guid id, CancellationToken cancellationToken)
    {
        var response = await _familyService.GetFamilyByIdAsync(CurrentUserId, id, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Actualiza el nombre o datos de un grupo familiar (Exclusivo para Super Admin).
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(FamilyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateFamily(Guid id, [FromBody] UpdateFamilyRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _familyService.UpdateFamilyAsync(CurrentUserId, id, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Elimina un grupo familiar y todas sus relaciones en cascada (Exclusivo para Super Admin).
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFamily(Guid id, CancellationToken cancellationToken)
    {
        await _familyService.DeleteFamilyAsync(CurrentUserId, id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Crea y da de alta una cuenta de usuario directamente dentro del grupo familiar (Solo Admin de Familia o Super Admin).
    /// </summary>
    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(typeof(FamilyDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateMemberUser(Guid id, [FromBody] CreateFamilyMemberUserRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _familyService.CreateMemberUserAsync(CurrentUserId, id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Vincula a un usuario preexistente a la familia mediante su número de teléfono (Solo Admin de Familia o Super Admin).
    /// </summary>
    [HttpPost("{id:guid}/members/link")]
    [ProducesResponseType(typeof(FamilyDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkMember(Guid id, [FromBody] AddFamilyMemberRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _familyService.AddMemberByPhoneAsync(CurrentUserId, id, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Elimina a un miembro del grupo familiar (Admin de Familia o Super Admin) o permite la salida voluntaria de un miembro.
    /// </summary>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        await _familyService.RemoveMemberAsync(CurrentUserId, id, userId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Actualiza el rol de un miembro dentro de la familia ('admin' o 'member') (Admin de Familia o Super Admin).
    /// </summary>
    [HttpPatch("{id:guid}/members/{userId:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMemberRole(Guid id, Guid userId, [FromBody] UpdateMemberRoleRequestDto request, CancellationToken cancellationToken)
    {
        await _familyService.UpdateMemberRoleAsync(CurrentUserId, id, userId, request, cancellationToken);
        return NoContent();
    }
}
