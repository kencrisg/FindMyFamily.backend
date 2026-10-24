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
    private readonly bool _mockEnabled;

    public FirebasePushNotificationService(IConfiguration configuration, ILogger<FirebasePushNotificationService> logger)
    {
        _logger = logger;

        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
        var credentialsPath = configuration["FIREBASE_CREDENTIALS_PATH"];
        var credentialsAvailable = !string.IsNullOrEmpty(credentialsPath) && File.Exists(credentialsPath);

        if (!credentialsAvailable && !isDevelopment)
        {
            // Fuera de desarrollo se falla al arrancar. El modo simulado devolvía "enviado
            // correctamente" sin enviar nada: en una app para localizar a un familiar, un
            // fallo silencioso es peor que un error visible.
            throw new InvalidOperationException(
                $"Las credenciales de Firebase no están disponibles en '{credentialsPath}'. " +
                "Son obligatorias fuera del entorno de desarrollo: sin ellas el Silent Push no se envía.");
        }

        try
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                if (credentialsAvailable)
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
                    _logger.LogWarning(
                        "FIREBASE_CREDENTIALS_PATH no fue proporcionado o el archivo no existe. " +
                        "Modo simulado activo: las peticiones de ubicación NO despertarán ningún dispositivo real.");
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

            if (!isDevelopment)
            {
                throw;
            }
        }

        // El mock solo se habilita en desarrollo, nunca por el simple hecho de faltar el archivo.
        _mockEnabled = isDevelopment && !_isFirebaseInitialized;
    }

    public async Task<bool> SendSilentLocationRequestAsync(string fcmToken, Guid requestId, Guid requesterId, Guid familyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fcmToken))
        {
            _logger.LogWarning("FCM Token está vacío. No se puede enviar Silent Push.");
            return false;
        }

        if (!_isFirebaseInitialized || FirebaseApp.DefaultInstance == null)
        {
            if (!_mockEnabled)
            {
                _logger.LogError("Firebase no está inicializado: no se puede enviar el Silent Push.");
                return false;
            }

            // Nunca se registra el token completo: es un identificador sensible del dispositivo.
            _logger.LogInformation("[DEV-MOCK] Silent Push simulado para el token {TokenPrefix}… (RequestId: {RequestId})",
                Redact(fcmToken), requestId);
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

    private static string Redact(string token) =>
        token.Length <= 6 ? "***" : token[..6];
}
