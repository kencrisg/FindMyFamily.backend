using FindMyFamily.Core.DTOs.Users;
using FindMyFamily.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FindMyFamily.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
public class UsersController : ApiControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    // =========================================================================
    // ENDPOINTS DE PERFIL (Cualquier usuario autenticado con Access Token)
    // =========================================================================

    /// <summary>
    /// Obtiene el perfil del usuario autenticado, incluyendo su SystemRole (tabla users) y las familias a las que pertenece con su respectivo FamilyRole (tabla family_members).
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
    [HttpPatch("me/phone")]
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
    [HttpPatch("me/password")]
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
    /// Lista todos los usuarios de la plataforma con sus roles y familias (Solo Super Administrador).
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
    /// Consulta el detalle de un usuario específico (Solo Super Administrador).
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.AdminGetUserByIdAsync(CurrentUserId, id, cancellationToken);
        return Ok(user);
    }

    /// <summary>
    /// Crea un nuevo usuario en la plataforma asignándolo obligatoriamente a una familia (Solo Super Administrador).
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpPost]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdminCreateUser([FromBody] AdminCreateUserRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _userService.AdminCreateUserAsync(CurrentUserId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Modifica los datos o reasigna de familia a un usuario existente (Solo Super Administrador).
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdminUpdateUser(Guid id, [FromBody] AdminUpdateUserRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _userService.AdminUpdateUserAsync(CurrentUserId, id, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Lista las cuentas eliminadas lógicamente (Solo Super Administrador).
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpGet("deleted")]
    [ProducesResponseType(typeof(IReadOnlyList<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDeletedUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var users = await _userService.AdminGetDeletedUsersAsync(CurrentUserId, page, pageSize, cancellationToken);
        return Ok(users);
    }

    /// <summary>
    /// Restaura una cuenta eliminada lógicamente (Solo Super Administrador).
    /// No la devuelve a ninguna familia: hay que reasignarla explícitamente.
    /// </summary>
    [Authorize(Roles = "admin")]
    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RestoreUser(Guid id, CancellationToken cancellationToken)
    {
        var response = await _userService.AdminRestoreUserAsync(CurrentUserId, id, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Elimina un usuario del sistema y todas sus dependencias en cascada (Solo Super Administrador).
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
