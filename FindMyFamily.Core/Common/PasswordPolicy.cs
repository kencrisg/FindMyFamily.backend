using FindMyFamily.Core.Exceptions;

namespace FindMyFamily.Core.Common;

/// <summary>
/// Política única de contraseñas. Todas las rutas que fijan una contraseña
/// (alta por superadmin, alta por admin familiar, cambio propio) deben pasar por aquí:
/// antes cada una validaba distinto o no validaba nada.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static void Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new BadRequestException("La contraseña es obligatoria.", "PASSWORD_REQUIRED");
        }

        if (password.Length < MinLength)
        {
            throw new BadRequestException(
                $"La contraseña debe tener al menos {MinLength} caracteres.", "PASSWORD_TOO_SHORT");
        }

        if (password.Length > MaxLength)
        {
            throw new BadRequestException(
                $"La contraseña no puede exceder {MaxLength} caracteres.", "PASSWORD_TOO_LONG");
        }

        var hasLetter = password.Any(char.IsLetter);
        var hasDigit = password.Any(char.IsDigit);

        if (!hasLetter || !hasDigit)
        {
            throw new BadRequestException(
                "La contraseña debe combinar al menos una letra y un número.", "PASSWORD_TOO_WEAK");
        }
    }
}
