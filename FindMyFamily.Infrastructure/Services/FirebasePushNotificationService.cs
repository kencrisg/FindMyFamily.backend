using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using FindMyFamily.Core.Interfaces.Services;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FindMyFamily.Infrastructure.Services;

public class FirebasePushNotificationService : IPushNotificationService
{
    private readonly ILogger<FirebasePushNotificationService> _logger;
    private readonly bool _isFirebaseInitialized;

    public FirebasePushNotificationService(IConfiguration configuration, ILogger<FirebasePushNotificationService> logger)
    {
        _logger = logger;

        try
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                var credentialsPath = configuration["FIREBASE_CREDENTIALS_PATH"];

                if (!string.IsNullOrEmpty(credentialsPath) && File.Exists(credentialsPath))
                {
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromFile(credentialsPath)
                    });
                    _isFirebaseInitialized = true;
                    _logger.LogInformation("Firebase Admin SDK inicializado correctamente desde {Path}", credentialsPath);
                }
                else
                {
                    _logger.LogWarning("FIREBASE_CREDENTIALS_PATH no fue proporcionado o el archivo no existe. Modo desarrollo simulado activo para FCM.");
                    _isFirebaseInitialized = false;
                }
            }
            else
            {
                _isFirebaseInitialized = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al inicializar Firebase Admin SDK.");
            _isFirebaseInitialized = false;
        }
    }

    public async Task<bool> SendSilentLocationRequestAsync(string fcmToken, Guid requestId, Guid requesterId, Guid familyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fcmToken))
        {
            _logger.LogWarning("FCM Token está vacío. No se puede enviar Silent Push.");
            return false;
        }

        // Si estamos en desarrollo sin credenciales reales, simulamos el envío exitoso
        if (!_isFirebaseInitialized || FirebaseApp.DefaultInstance == null)
        {
            _logger.LogInformation("[DEV-MOCK] Silent Push simulado para token: {Token}, RequestId: {RequestId}, RequesterId: {RequesterId}",
                fcmToken, requestId, requesterId);
            return true;
        }

        try
        {
            // Silent Push: Data Message puro SIN bloque 'Notification' visual
            var message = new Message
            {
                Token = fcmToken,
                Data = new Dictionary<string, string>
                {
                    { "type", "LOCATION_REQUEST" },
                    { "request_id", requestId.ToString() },
                    { "requester_id", requesterId.ToString() },
                    { "family_id", familyId.ToString() },
                    { "timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() }
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High
                },
                Apns = new ApnsConfig
                {
                    Headers = new Dictionary<string, string>
                    {
                        { "apns-push-type", "background" },
                        { "apns-priority", "5" }
                    },
                    Aps = new Aps
                    {
                        ContentAvailable = true
                    }
                }
            };

            var response = await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);
            _logger.LogInformation("Silent Push enviado exitosamente. FCM Message ID: {MessageId}", response);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar Silent Push a través de Firebase Messaging.");
            return false;
        }
    }
}
