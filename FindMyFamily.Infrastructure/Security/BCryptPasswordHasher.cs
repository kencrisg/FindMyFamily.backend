using FindMyFamily.Core.Interfaces.Services;

namespace FindMyFamily.Infrastructure.Security;

public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    /// <summary>
    /// Hash señuelo precalculado de una contraseña que nadie conoce. Verificar contra él
    /// cuesta lo mismo que una verificación real, que es justo el objetivo.
    /// </summary>
    private static readonly string DummyHash =
        BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), WorkFactor);

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Un hash corrupto en base de datos no debe tumbar la petición con un 500.
            return false;
        }
    }

    public bool VerifyDummy(string password)
    {
        BCrypt.Net.BCrypt.Verify(password, DummyHash);
        return false;
    }
}
