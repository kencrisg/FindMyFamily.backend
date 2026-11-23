using FindMyFamily.Core.Exceptions;

namespace FindMyFamily.Core.Common;

/// <summary>
/// Validación única de nombres y apellidos. Antes solo se comprobaba que no estuvieran
/// vacíos: un nombre de más de 50 caracteres llegaba a la base de datos, que lo rechazaba
/// por la longitud de la columna, y la API respondía con un 500 en lugar de un 400 claro.
/// </summary>
public static class PersonName
{
    /// <summary>Debe coincidir con HasMaxLength de first_name y last_name.</summary>
    public const int MaxLength = 50;

    /// <param name="value">Valor recibido.</param>
    /// <param name="field">Nombre del campo para el mensaje: "nombre" o "apellido".</param>
    public static string Normalize(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BadRequestException($"El {field} es obligatorio.", "INVALID_INPUT");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new BadRequestException(
                $"El {field} no puede exceder {MaxLength} caracteres.", "NAME_TOO_LONG");
        }

        return trimmed;
    }
}
