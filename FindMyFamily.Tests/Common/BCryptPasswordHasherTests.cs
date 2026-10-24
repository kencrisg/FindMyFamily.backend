using FindMyFamily.Infrastructure.Security;

namespace FindMyFamily.Tests.Common;

public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_NoDevuelveLaContrasenaEnClaro()
    {
        var hash = _hasher.HashPassword("Valida2026");

        Assert.DoesNotContain("Valida2026", hash);
        Assert.StartsWith("$2", hash);
    }

    [Fact]
    public void HashPassword_UsaSaltDistintoCadaVez()
    {
        Assert.NotEqual(_hasher.HashPassword("Valida2026"), _hasher.HashPassword("Valida2026"));
    }

    [Fact]
    public void VerifyPassword_AceptaLaCorrectaYRechazaLaIncorrecta()
    {
        var hash = _hasher.HashPassword("Valida2026");

        Assert.True(_hasher.VerifyPassword("Valida2026", hash));
        Assert.False(_hasher.VerifyPassword("Otra2026", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-hash-bcrypt")]
    public void VerifyPassword_ConHashCorruptoDevuelveFalseSinLanzar(string hashCorrupto)
    {
        // Un hash inválido en base de datos no debe tumbar la petición con un 500.
        Assert.False(_hasher.VerifyPassword("Valida2026", hashCorrupto));
    }

    [Fact]
    public void VerifyDummy_SiempreDevuelveFalse()
    {
        Assert.False(_hasher.VerifyDummy("cualquiera"));
    }
}
