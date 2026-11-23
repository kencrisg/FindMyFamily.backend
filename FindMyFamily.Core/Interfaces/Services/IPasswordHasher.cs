namespace FindMyFamily.Core.Interfaces.Services;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);

    /// <summary>
    /// Verifica la contraseña contra un hash señuelo y devuelve siempre false.
    /// Sirve para que el login consuma el mismo tiempo cuando el teléfono no existe
    /// que cuando existe, evitando enumerar usuarios por diferencia de latencia.
    /// </summary>
    bool VerifyDummy(string password);
}
