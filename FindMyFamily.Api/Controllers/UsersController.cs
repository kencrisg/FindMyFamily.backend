using System.Security.Claims;
using FindMyFamily.Core.DTOs.Users;
using FindMyFamily.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FindMyFamily.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
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

    // =========================================================================
    // ENDPOINTS DE PERFIL (Cualquier usuario autenticado con Access Token)
    // =========================================================================

    /// <summary>
    /// Obtiene el perfil del usuario autenticado.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var profile = await _userService.GetProfileAsync(CurrentUserId, cancellationToken);
        return Ok(profile);
    }

    /// <summary>
    /// Actualiza el número de teléfono del usuario autenticado (requiere contraseña por seguridad).
    /// </summary>
    [HttpPut("me/phone")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdatePhone([FromBody] UpdatePhoneRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _userService.UpdatePhoneNumberAsync(CurrentUserId, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Actualiza la contraseña del usuario autenticado (requiere contraseña actual).
    /// </summary>
    [HttpPut("me/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _userService.UpdatePasswordAsync(CurrentUserId, request, cancellationToken);
        return NoContent();
    }

    // =========================================================================
    // ENDPOINTS DE ADMINISTRADOR DE SISTEMA ([Authorize(Roles = "admin")])
    // =========================================================================

    /// <summary>
    /// Lista todos los usuarios de la plataforma (Solo Administradores del Sistema).
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var users = await _userService.AdminGetAllUsersAsync(CurrentUserId, page, pageSize, cancellationToken);
        return Ok(users);
    }

    /// <summary>
    /// Crea un nuevo usuario directamente desde el panel de administración (Solo Administradores del Sistema).
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpPost]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdminCreateUser([FromBody] AdminCreateUserRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _userService.AdminCreateUserAsync(CurrentUserId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Elimina un usuario del sistema (Solo Administradores del Sistema).
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdminDeleteUser(Guid id, CancellationToken cancellationToken)
    {
        await _userService.AdminDeleteUserAsync(CurrentUserId, id, cancellationToken);
        return NoContent();
    }
}
