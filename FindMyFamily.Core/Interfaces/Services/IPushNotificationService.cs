namespace FindMyFamily.Core.Interfaces.Services;

public interface IPushNotificationService
{
    /// <summary>
    /// Envía un Silent Push (Data message sin alerta visual) a través de Firebase Cloud Messaging
    /// para despertar la aplicación en segundo plano en el dispositivo destino y capturar su GPS.
    /// </summary>
    Task<bool> SendSilentLocationRequestAsync(string fcmToken, Guid requestId, Guid requesterId, Guid familyId, CancellationToken cancellationToken = default);
}
