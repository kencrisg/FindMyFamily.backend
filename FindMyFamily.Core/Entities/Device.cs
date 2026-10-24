namespace FindMyFamily.Core.Entities;

public class Device
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// Identificador estable de la instalación de la app, generado por el cliente y
    /// persistido en su almacenamiento seguro. Es la clave real de un dispositivo:
    /// identificarlo por <see cref="DeviceModel"/> hacía que dos teléfonos del mismo
    /// modelo compartieran fila y se robaran mutuamente la sesión y el token FCM.
    /// </summary>
    public string? InstallationId { get; set; }

    public string? FcmToken { get; set; }

    /// <summary>Hash SHA-256 del refresh token. Nunca se guarda el token en claro.</summary>
    public string? RefreshTokenHash { get; set; }

    public DateTime? RefreshTokenExpiry { get; set; }
    public string? DeviceModel { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public User User { get; set; } = null!;
}
