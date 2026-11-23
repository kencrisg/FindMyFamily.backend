namespace FindMyFamily.Core.Interfaces.Services;

/// <summary>
/// Hash determinista (SHA-256) del refresh token. Determinista y no BCrypt porque
/// el token es un secreto de 64 bytes de entropía criptográfica: no necesita salt
/// ni coste de trabajo, y sí necesita ser buscable por igualdad en la base de datos.
/// </summary>
public interface IRefreshTokenHasher
{
    string Hash(string refreshToken);
}
