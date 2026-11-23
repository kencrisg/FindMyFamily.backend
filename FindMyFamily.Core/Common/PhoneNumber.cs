using System.Text.RegularExpressions;
using FindMyFamily.Core.Exceptions;

namespace FindMyFamily.Core.Common;

/// <summary>
/// Normalización y validación única del número telefónico, que es la identidad
/// de login del sistema. Antes cada servicio hacía solo `.Trim()`, de modo que
/// "0995929826" y "+593 995929826" convivían como cuentas distintas.
/// </summary>
public static class PhoneNumber
{
    // Dígitos, con un '+' opcional al inicio. 7 a 19 dígitos (la columna admite 20 caracteres).
    private static readonly Regex Allowed = new(@"^\+?[0-9]{7,19}$", RegexOptions.Compiled);

    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new BadRequestException("El número de teléfono es obligatorio.", "PHONE_REQUIRED");
        }

        // Elimina separadores habituales de la entrada humana: espacios, guiones y paréntesis.
        var cleaned = new string(raw.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '(' && c != ')').ToArray());

        if (!Allowed.IsMatch(cleaned))
        {
            throw new BadRequestException(
                "El número de teléfono no tiene un formato válido.", "INVALID_PHONE_NUMBER");
        }

        return cleaned;
    }
}
