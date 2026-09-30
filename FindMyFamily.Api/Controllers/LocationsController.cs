using System.Security.Claims;
using FindMyFamily.Core.DTOs.Locations;
using FindMyFamily.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FindMyFamily.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private readonly ILocationService _locationService;

    public LocationsController(ILocationService locationService)
    {
        _locationService = locationService;
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

    /// <summary>
    /// Solicita la ubicación On-Demand de un familiar (emite un Silent Push vía FCM para despertar su celular).
    /// </summary>
    [HttpPost("request")]
    [ProducesResponseType(typeof(LocationRequestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestLocation([FromBody] RequestLocationDto request, CancellationToken cancellationToken)
    {
        var response = await _locationService.RequestLocationAsync(CurrentUserId, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// El dispositivo destino reporta sus coordenadas GPS tras ser despertado por el Silent Push.
    /// El backend almacena la coordenada y la retransmite en tiempo real vía SignalR a su familia.
    /// </summary>
    [HttpPost("report")]
    [ProducesResponseType(typeof(LocationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReportLocation([FromBody] ReportLocationDto request, CancellationToken cancellationToken)
    {
        var response = await _locationService.ReportLocationAsync(CurrentUserId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Obtiene la última ubicación GPS registrada de un miembro de la familia.
    /// </summary>
    [HttpGet("families/{familyId:guid}/users/{userId:guid}/latest")]
    [ProducesResponseType(typeof(LocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestLocation(Guid familyId, Guid userId, CancellationToken cancellationToken)
    {
        var response = await _locationService.GetLatestLocationAsync(CurrentUserId, familyId, userId, cancellationToken);
        if (response == null)
        {
            return NotFound(new { error = true, code = "NO_LOCATION_DATA", message = "No hay registros de ubicación recientes para este usuario." });
        }
        return Ok(response);
    }

    /// <summary>
    /// Obtiene el historial reciente de ubicaciones de un miembro de la familia.
    /// </summary>
    [HttpGet("families/{familyId:guid}/users/{userId:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<LocationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLocationHistory(Guid familyId, Guid userId, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        var response = await _locationService.GetLocationHistoryAsync(CurrentUserId, familyId, userId, limit, cancellationToken);
        return Ok(response);
    }
}
