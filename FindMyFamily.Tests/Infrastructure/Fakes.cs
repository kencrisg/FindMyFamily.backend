using FindMyFamily.Core.DTOs.Locations;
using FindMyFamily.Core.Entities;
using FindMyFamily.Core.Interfaces.Services;

namespace FindMyFamily.Tests.Infrastructure;

/// <summary>
/// Hasher determinista y rápido. BCrypt con factor de trabajo 12 tarda ~250 ms por hash:
/// usarlo en los tests multiplicaría su duración sin probar nada propio del dominio.
/// La verificación real de BCrypt se cubre aparte, en BCryptPasswordHasherTests.
/// </summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public const string Prefix = "hashed:";

    public string HashPassword(string password) => Prefix + password;

    public bool VerifyPassword(string password, string passwordHash) =>
        passwordHash == Prefix + password;

    public bool VerifyDummy(string password) => false;
}

public sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
{
    public int RefreshTokensGenerated { get; private set; }

    public string GenerateAccessToken(User user, Device device) =>
        $"access-token:{user.Id}:{device.Id}";

    public string GenerateRefreshToken()
    {
        RefreshTokensGenerated++;
        return $"refresh-token-{RefreshTokensGenerated}-{Guid.NewGuid():N}";
    }
}

public sealed class FakeRefreshTokenHasher : IRefreshTokenHasher
{
    public string Hash(string refreshToken) => "sha:" + refreshToken;
}

/// <summary>Registra los Silent Push emitidos y permite simular que el envío falla.</summary>
public sealed class FakePushNotificationService : IPushNotificationService
{
    public record Sent(string FcmToken, Guid RequestId, Guid RequesterId, Guid FamilyId);

    public List<Sent> SentMessages { get; } = new();
    public bool ShouldSucceed { get; set; } = true;

    public Task<bool> SendSilentLocationRequestAsync(
        string fcmToken, Guid requestId, Guid requesterId, Guid familyId,
        CancellationToken cancellationToken = default)
    {
        if (ShouldSucceed)
        {
            SentMessages.Add(new Sent(fcmToken, requestId, requesterId, familyId));
        }

        return Task.FromResult(ShouldSucceed);
    }
}

/// <summary>Captura a quién se difundió cada coordenada, que es el punto sensible: una
/// ubicación no debe llegar a nadie fuera de la familia que la pidió.</summary>
public sealed class FakeLocationRealtimeNotifier : ILocationRealtimeNotifier
{
    public record Broadcast(IReadOnlyList<Guid> RecipientUserIds, LocationDto Location);

    public List<Broadcast> Broadcasts { get; } = new();

    public Task SendLocationUpdateAsync(
        IReadOnlyList<Guid> recipientUserIds, LocationDto location,
        CancellationToken cancellationToken = default)
    {
        Broadcasts.Add(new Broadcast(recipientUserIds.ToList(), location));
        return Task.CompletedTask;
    }
}
