using System.Security.Claims;
using FindMyFamily.Core.DTOs.Devices;
using FindMyFamily.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FindMyFamily.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;

    public DevicesController(IDeviceService deviceService)
    {
        _deviceService = deviceService;
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

    private Guid? CurrentDeviceId
    {
        get
        {
            var claim = User.FindFirst("device_id");
            if (claim != null && Guid.TryParse(claim.Value, out var deviceId))
            {
                return deviceId;
            }
            return null;
        }
    }

    /// <summary>
    /// Actualiza el token de Firebase (FCM Token) del dispositivo actual para recibir notificaciones Silent Push.
    /// Invocado automáticamente por la app móvil cuando Firebase rota el token.
    /// </summary>
    [HttpPut("fcm-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateFcmToken([FromBody] UpdateFcmTokenRequestDto request, CancellationToken cancellationToken)
    {
        await _deviceService.UpdateFcmTokenAsync(CurrentUserId, CurrentDeviceId, request, cancellationToken);
        return NoContent();
    }
}
