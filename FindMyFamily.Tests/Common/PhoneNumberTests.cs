using FindMyFamily.Core.Common;
using FindMyFamily.Core.Exceptions;

namespace FindMyFamily.Tests.Common;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("0999000001", "0999000001")]
    [InlineData("  0999000001  ", "0999000001")]
    [InlineData("099-900-0001", "0999000001")]
    [InlineData("(099) 900 0001", "0999000001")]
    [InlineData("+593995929826", "+593995929826")]
    [InlineData("+593 99 592 9826", "+593995929826")]
    public void Normalize_LimpiaSeparadoresYConservaElPrefijo(string entrada, string esperado)
    {
        Assert.Equal(esperado, PhoneNumber.Normalize(entrada));
    }

    [Theory]
    [InlineData(null, "PHONE_REQUIRED")]
    [InlineData("", "PHONE_REQUIRED")]
    [InlineData("   ", "PHONE_REQUIRED")]
    [InlineData("abc", "INVALID_PHONE_NUMBER")]
    [InlineData("099900000a", "INVALID_PHONE_NUMBER")]
    [InlineData("123456", "INVALID_PHONE_NUMBER")]
    [InlineData("099900000199990001999900019", "INVALID_PHONE_NUMBER")]
    [InlineData("099+9000001", "INVALID_PHONE_NUMBER")]
    public void Normalize_RechazaConElCodigoCorrecto(string? entrada, string codigoEsperado)
    {
        var ex = Assert.Throws<BadRequestException>(() => PhoneNumber.Normalize(entrada));
        Assert.Equal(codigoEsperado, ex.Code);
    }

    [Fact]
    public void Normalize_DosFormatosDelMismoNumeroDanElMismoResultado()
    {
        // Es lo que evita que "099-900-0001" y "0999000001" convivan como cuentas distintas.
        Assert.Equal(PhoneNumber.Normalize("0999000001"), PhoneNumber.Normalize("099 900 0001"));
    }
}
