using System.Security.Claims;
using FindMyFamily.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace FindMyFamily.Api.Controllers;

/// <summary>
/// Base común para la lectura de la identidad del llamante. Cada controlador repetía esta
/// lógica y lanzaba `UnauthorizedAccessException`, que al no derivar de `AppException` el
/// middleware clasificaba como error interno: un token sin claims devolvía 500 en lugar de 401.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid CurrentUserId
    {
        get
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            {
                throw new UnauthorizedException("Usuario no autenticado.", "UNAUTHENTICATED");
            }
            return userId;
        }
    }

    protected Guid? CurrentDeviceId
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

    protected bool IsSuperAdmin => User.IsInRole("admin");
}
