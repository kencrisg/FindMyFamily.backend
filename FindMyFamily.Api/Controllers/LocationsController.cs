using FindMyFamily.Core.DTOs.Locations;
using FindMyFamily.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FindMyFamily.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
public class LocationsController : ApiControllerBase
{
    private readonly ILocationService _locationService;

    public LocationsController(ILocationService locationService)
    {
        _locationService = locationService;
    }

    /// <summary>
    /// Solicita la ubicación On-Demand de un familiar (emite un Silent Push vía FCM para despertar su celular).
    /// </summary>
    [EnableRateLimiting("location-request")]
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
    /// Borra todas las ubicaciones almacenadas del usuario autenticado (derecho de supresión).
    /// La app solo muestra la última posición, pero el rastro queda guardado: esto permite
    /// al propio usuario eliminarlo.
    /// </summary>
    [HttpDelete("me/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteMyHistory(CancellationToken cancellationToken)
    {
        var deleted = await _locationService.DeleteMyHistoryAsync(CurrentUserId, cancellationToken);
        return Ok(new { deleted });
    }
}
