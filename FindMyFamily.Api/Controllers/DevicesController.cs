using FindMyFamily.Core.DTOs.Devices;
using FindMyFamily.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FindMyFamily.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
public class DevicesController : ApiControllerBase
{
    private readonly IDeviceService _deviceService;

    public DevicesController(IDeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    /// <summary>
    /// Actualiza el token de Firebase (FCM Token) del dispositivo actual para recibir notificaciones Silent Push.
    /// Invocado automáticamente por la app móvil cuando Firebase rota el token.
    /// </summary>
    [HttpPatch("fcm-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateFcmToken([FromBody] UpdateFcmTokenRequestDto request, CancellationToken cancellationToken)
    {
        await _deviceService.UpdateFcmTokenAsync(CurrentUserId, CurrentDeviceId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Desvincula o elimina un dispositivo/teléfono registrado (Solo Super Admin o el propio usuario dueño del dispositivo).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkDevice(Guid id, CancellationToken cancellationToken)
    {
        await _deviceService.UnlinkDeviceAsync(CurrentUserId, id, cancellationToken);
        return NoContent();
    }
}
