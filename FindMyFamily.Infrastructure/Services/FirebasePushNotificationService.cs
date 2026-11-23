using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using FindMyFamily.Core.Interfaces.Services;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

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
        // En un despliegue no hay forma cómoda de subir un archivo: las credenciales llegan
        // por variable de entorno. En local se sigue usando el archivo.
        var credentialsJson = ReadCredentialsFromEnvironment(configuration);
        var credentialsPath = credentialsJson == null
            ? ResolveCredentialsPath(configuration["FIREBASE_CREDENTIALS_PATH"])
            : null;
        var credentialsAvailable = credentialsJson != null || credentialsPath != null;

        if (!credentialsAvailable && !isDevelopment)
        {
            // Fuera de desarrollo se falla al arrancar. El modo simulado devolvía "enviado
            // correctamente" sin enviar nada: en una app para localizar a un familiar, un
            // fallo silencioso es peor que un error visible.
            throw new InvalidOperationException(
                "Las credenciales de Firebase no están disponibles. Define FIREBASE_CREDENTIALS_JSON " +
                "(contenido del JSON, en claro o en base64) o FIREBASE_CREDENTIALS_PATH apuntando al archivo. " +
                "Son obligatorias fuera del entorno de desarrollo: sin ellas el Silent Push no se envía.");
        }

        try
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                if (credentialsJson != null)
                {
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromJson(credentialsJson)
                    });
                    _isFirebaseInitialized = true;
                    _logger.LogInformation("Firebase Admin SDK inicializado desde la variable de entorno.");
                }
                else if (credentialsPath != null)
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


    /// <summary>
    /// Lee las credenciales de la variable FIREBASE_CREDENTIALS_JSON.
    ///
    /// Acepta el JSON en claro o codificado en base64, porque muchos paneles de
    /// configuración estropean los saltos de línea de la clave privada y el base64 es la
    /// forma fiable de pegarla sin que se corrompa.
    /// </summary>
    private static string? ReadCredentialsFromEnvironment(IConfiguration configuration)
    {
        var valor = configuration["FIREBASE_CREDENTIALS_JSON"];
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        valor = valor.Trim();

        if (valor.StartsWith('{'))
        {
            return valor;
        }

        try
        {
            var decodificado = Encoding.UTF8.GetString(Convert.FromBase64String(valor));
            return decodificado.TrimStart().StartsWith('{') ? decodificado : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Resuelve la ruta de las credenciales.
    ///
    /// Un valor relativo como "./firebase-admin.json" se interpreta contra el directorio de
    /// trabajo, que al ejecutar `dotnet run --project FindMyFamily.Api` es la carpeta del
    /// proyecto de API y no la raíz del backend, donde está el archivo. El resultado era que
    /// el SDK nunca se inicializaba y el servicio se quedaba en modo simulado: respondía
    /// "enviado correctamente" sin enviar nada. Por eso se busca también hacia arriba en el
    /// árbol de directorios, el mismo criterio que usa DotNetEnv para encontrar el `.env`.
    ///
    /// Devuelve la ruta absoluta encontrada, o null si no existe en ningún nivel.
    /// </summary>
    private static string? ResolveCredentialsPath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return File.Exists(configuredPath) ? configuredPath : null;
        }

        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory != null)
        {
            var candidate = Path.GetFullPath(Path.Combine(directory.FullName, configuredPath));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string Redact(string token) =>
        token.Length <= 6 ? "***" : token[..6];
}
