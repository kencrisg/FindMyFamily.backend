using FindMyFamily.Core.Common;
using FindMyFamily.Core.Exceptions;

namespace FindMyFamily.Tests.Common;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Valida2026")]
    [InlineData("abc12345")]
    [InlineData("8caracte1")]
    public void Validate_AceptaContrasenasConLetrasYNumeros(string password)
    {
        PasswordPolicy.Validate(password);
    }

    [Theory]
    [InlineData(null, "PASSWORD_REQUIRED")]
    [InlineData("", "PASSWORD_REQUIRED")]
    [InlineData("   ", "PASSWORD_REQUIRED")]
    [InlineData("abc123", "PASSWORD_TOO_SHORT")]
    [InlineData("solotexto", "PASSWORD_TOO_WEAK")]
    [InlineData("12345678", "PASSWORD_TOO_WEAK")]
    public void Validate_RechazaConElCodigoCorrecto(string? password, string codigoEsperado)
    {
        var ex = Assert.Throws<BadRequestException>(() => PasswordPolicy.Validate(password));
        Assert.Equal(codigoEsperado, ex.Code);
    }

    [Fact]
    public void Validate_RechazaContrasenasDemasiadoLargas()
    {
        var password = new string('a', 120) + "12345678901234567890";

        var ex = Assert.Throws<BadRequestException>(() => PasswordPolicy.Validate(password));
        Assert.Equal("PASSWORD_TOO_LONG", ex.Code);
    }
}
