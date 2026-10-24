using FindMyFamily.Core.DTOs.Auth;
using FindMyFamily.Core.Exceptions;
using FindMyFamily.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FindMyFamily.Tests.Services;

public class AuthServiceTests
{
    private static LoginRequestDto Login(
        string phone = "0999000001",
        string password = "Valida2026",
        string? installationId = "instalacion-A",
        string? deviceModel = "Apple iPhone",
        string? fcmToken = null)
        => new(phone, password, installationId, deviceModel, fcmToken);

    [Fact]
    public async Task Login_ConCredencialesCorrectas_DevuelveTokensYCreaDispositivo()
    {
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000001", password: "Valida2026");

        var result = await h.Auth.LoginAsync(Login(fcmToken: "fcm-abc"));

        Assert.Equal(user.Id, result.User.Id);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);

        var device = await h.Db.Devices.SingleAsync();
        Assert.Equal("instalacion-A", device.InstallationId);
        Assert.Equal("fcm-abc", device.FcmToken);
    }

    [Fact]
    public async Task Login_NoDevuelveElTokenFcmAlCliente()
    {
        await using var h = ServiceHarness.Create();
        await h.AddUserAsync("0999000001");

        var result = await h.Auth.LoginAsync(Login(fcmToken: "fcm-secreto"));

        // El token FCM identifica al dispositivo; el servidor no tiene por qué devolverlo.
        Assert.DoesNotContain("fcm-secreto", result.Device.ToString());
    }

    [Fact]
    public async Task Login_GuardaElRefreshTokenHasheadoNoEnClaro()
    {
        await using var h = ServiceHarness.Create();
        await h.AddUserAsync("0999000001");

        var result = await h.Auth.LoginAsync(Login());

        var device = await h.Db.Devices.SingleAsync();
        Assert.NotEqual(result.RefreshToken, device.RefreshTokenHash);
        Assert.Equal(h.RefreshTokenHasher.Hash(result.RefreshToken), device.RefreshTokenHash);
    }

    [Theory]
    [InlineData("0999000001", "ContrasenaMala1")]
    [InlineData("0999999999", "Valida2026")]
    public async Task Login_ConCredencialesInvalidas_Lanza401(string phone, string password)
    {
        await using var h = ServiceHarness.Create();
        await h.AddUserAsync("0999000001", password: "Valida2026");

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => h.Auth.LoginAsync(Login(phone, password)));

        // El mismo código y mensaje en ambos casos: no se revela si el teléfono existe.
        Assert.Equal("INVALID_CREDENTIALS", ex.Code);
    }

    [Fact]
    public async Task Login_DeUnaCuentaBorradaLogicamente_Lanza401()
    {
        await using var h = ServiceHarness.Create();
        await h.AddUserAsync("0999000001", deletedAt: DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => h.Auth.LoginAsync(Login()));
        Assert.Equal("INVALID_CREDENTIALS", ex.Code);
    }

    [Fact]
    public async Task Login_NormalizaElTelefonoAntesDeBuscar()
    {
        await using var h = ServiceHarness.Create();
        await h.AddUserAsync("0999000001");

        var result = await h.Auth.LoginAsync(Login(phone: "099-900-0001"));

        Assert.Equal("0999000001", result.User.PhoneNumber);
    }

    [Fact]
    public async Task Login_DosDispositivosDelMismoModelo_MantienenSesionesSeparadas()
    {
        // Esta es la regresión que motivó el campo InstallationId: antes el dispositivo se
        // identificaba por su modelo, así que el segundo teléfono robaba la sesión al primero.
        await using var h = ServiceHarness.Create();
        await h.AddUserAsync("0999000001");

        var primero = await h.Auth.LoginAsync(Login(installationId: "telefono-1", deviceModel: "Apple iPhone"));
        var segundo = await h.Auth.LoginAsync(Login(installationId: "telefono-2", deviceModel: "Apple iPhone"));

        Assert.NotEqual(primero.Device.Id, segundo.Device.Id);
        Assert.Equal(2, await h.Db.Devices.CountAsync());

        // El refresh token del primer teléfono sigue siendo válido.
        var renovado = await h.Auth.RefreshTokenAsync(new RefreshTokenRequestDto(primero.RefreshToken, "telefono-1", null, null));
        Assert.NotEmpty(renovado.AccessToken);
    }

    [Fact]
    public async Task Login_ConElMismoInstallationId_ReutilizaElDispositivo()
    {
        await using var h = ServiceHarness.Create();
        await h.AddUserAsync("0999000001");

        var primero = await h.Auth.LoginAsync(Login(installationId: "telefono-1"));
        var segundo = await h.Auth.LoginAsync(Login(installationId: "telefono-1"));

        Assert.Equal(primero.Device.Id, segundo.Device.Id);
        Assert.Equal(1, await h.Db.Devices.CountAsync());
    }

    [Fact]
    public async Task RefreshToken_RotaElTokenEInvalidaElAnterior()
    {
        await using var h = ServiceHarness.Create();
        await h.AddUserAsync("0999000001");
        var sesion = await h.Auth.LoginAsync(Login());

        var renovado = await h.Auth.RefreshTokenAsync(
            new RefreshTokenRequestDto(sesion.RefreshToken, "instalacion-A", null, null));

        Assert.NotEqual(sesion.RefreshToken, renovado.RefreshToken);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => h.Auth.RefreshTokenAsync(new RefreshTokenRequestDto(sesion.RefreshToken, null, null, null)));
        Assert.Equal("INVALID_REFRESH_TOKEN", ex.Code);
    }

    [Fact]
    public async Task RefreshToken_CaducadoLanzaElCodigoCorrespondiente()
    {
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000001");
        await h.AddDeviceAsync(user.Id, refreshToken: "token-viejo",
            refreshTokenExpiry: DateTime.UtcNow.AddDays(-1));

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => h.Auth.RefreshTokenAsync(new RefreshTokenRequestDto("token-viejo", null, null, null)));

        Assert.Equal("EXPIRED_REFRESH_TOKEN", ex.Code);
    }

    [Fact]
    public async Task RefreshToken_DeUnDispositivoDesactivadoLanza401()
    {
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000001");
        await h.AddDeviceAsync(user.Id, isActive: false, refreshToken: "token",
            refreshTokenExpiry: DateTime.UtcNow.AddDays(30));

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => h.Auth.RefreshTokenAsync(new RefreshTokenRequestDto("token", null, null, null)));

        Assert.Equal("INVALID_REFRESH_TOKEN", ex.Code);
    }

    [Fact]
    public async Task RefreshToken_VacioLanza400()
    {
        await using var h = ServiceHarness.Create();

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => h.Auth.RefreshTokenAsync(new RefreshTokenRequestDto("  ", null, null, null)));

        Assert.Equal("INVALID_REFRESH_TOKEN", ex.Code);
    }

    [Fact]
    public async Task Logout_RevocaElRefreshTokenEnElServidor()
    {
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000001");
        var sesion = await h.Auth.LoginAsync(Login());

        await h.Auth.LogoutAsync(user.Id, new LogoutRequestDto(sesion.RefreshToken));

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => h.Auth.RefreshTokenAsync(new RefreshTokenRequestDto(sesion.RefreshToken, null, null, null)));
        Assert.Equal("INVALID_REFRESH_TOKEN", ex.Code);
    }

    [Fact]
    public async Task Logout_NoPuedeRevocarLaSesionDeOtroUsuario()
    {
        await using var h = ServiceHarness.Create();
        await h.AddUserAsync("0999000001");
        var otro = await h.AddUserAsync("0999000002");
        var sesionVictima = await h.Auth.LoginAsync(Login());

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => h.Auth.LogoutAsync(otro.Id, new LogoutRequestDto(sesionVictima.RefreshToken)));

        Assert.Equal("INVALID_REFRESH_TOKEN", ex.Code);
    }

    [Fact]
    public async Task Logout_ConAllDevices_RevocaTodasLasSesiones()
    {
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000001");
        var uno = await h.Auth.LoginAsync(Login(installationId: "telefono-1"));
        var dos = await h.Auth.LoginAsync(Login(installationId: "telefono-2"));

        await h.Auth.LogoutAsync(user.Id, new LogoutRequestDto(null, AllDevices: true));

        foreach (var token in new[] { uno.RefreshToken, dos.RefreshToken })
        {
            await Assert.ThrowsAsync<UnauthorizedException>(
                () => h.Auth.RefreshTokenAsync(new RefreshTokenRequestDto(token, null, null, null)));
        }
    }

    [Fact]
    public async Task Logout_SinTokenNiAllDevices_Lanza400()
    {
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000001");

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => h.Auth.LogoutAsync(user.Id, new LogoutRequestDto(null)));

        Assert.Equal("REFRESH_TOKEN_REQUIRED", ex.Code);
    }
}
