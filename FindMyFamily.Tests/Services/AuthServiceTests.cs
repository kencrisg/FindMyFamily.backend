using FindMyFamily.Core.DTOs.Auth;
using FindMyFamily.Core.Exceptions;
using FindMyFamily.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
        await h.AddUserAsync("0999000001", isDeleted: true);

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
    public async Task Login_EnUnSegundoDispositivo_EliminaElRegistroDelAnterior()
    {
        // Una cuenta tiene un solo teléfono: al entrar desde otro, el registro anterior
        // se elimina para que el backend no siga considerándolo alcanzable.
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000001");

        var primero = await h.Auth.LoginAsync(Login(installationId: "telefono-1", fcmToken: "fcm-1"));
        var segundo = await h.Auth.LoginAsync(Login(installationId: "telefono-2", fcmToken: "fcm-2"));

        Assert.NotEqual(primero.Device.Id, segundo.Device.Id);

        var device = Assert.Single(await h.ReloadDevicesOfUserAsync(user.Id));
        Assert.Equal(segundo.Device.Id, device.Id);
        Assert.Equal("telefono-2", device.InstallationId);

        // El refresh token del teléfono reemplazado queda inservible.
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => h.Auth.RefreshTokenAsync(new RefreshTokenRequestDto(primero.RefreshToken, "telefono-1", null, null)));
        Assert.Equal("INVALID_REFRESH_TOKEN", ex.Code);

        Assert.Contains(h.AuthLog.Entries, e =>
            e.Level == LogLevel.Information
            && e.Message.Contains("dispositivo anterior")
            && e.Message.Contains("telefono-1")
            && e.Message.Contains("telefono-2"));
    }

    [Fact]
    public async Task Login_ConUnInstallationIdDeOtraCuenta_ReasignaElTelefono()
    {
        // Un teléfono no puede estar vinculado a dos usuarios. Si se inicia sesión con otra
        // cuenta en la misma instalación, el registro del usuario anterior se elimina.
        await using var h = ServiceHarness.Create();
        var primero = await h.AddUserAsync("0999000001");
        var segundo = await h.AddUserAsync("0999000002");

        await h.Auth.LoginAsync(Login(phone: "0999000001", installationId: "telefono-compartido"));
        var sesion = await h.Auth.LoginAsync(Login(phone: "0999000002", installationId: "telefono-compartido"));

        Assert.Empty(await h.ReloadDevicesOfUserAsync(primero.Id));

        var device = Assert.Single(await h.ReloadDevicesOfUserAsync(segundo.Id));
        Assert.Equal(sesion.Device.Id, device.Id);
        Assert.Equal("telefono-compartido", device.InstallationId);

        Assert.Contains(h.AuthLog.Entries, e =>
            e.Level == LogLevel.Information
            && e.Message.Contains("estaba vinculado al usuario")
            && e.Message.Contains("telefono-compartido"));
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
    public async Task Logout_EliminaElDispositivoDeLaTablaDevices()
    {
        // Cerrar sesión debe borrar el registro, no solo desactivarlo: el backend no debe
        // conservar ni el token FCM ni el refresh token de un teléfono que ya salió.
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000001");
        var sesion = await h.Auth.LoginAsync(Login(fcmToken: "fcm-activo"));

        await h.Auth.LogoutAsync(user.Id, new LogoutRequestDto(sesion.RefreshToken));

        Assert.Empty(await h.ReloadDevicesOfUserAsync(user.Id));

        Assert.Contains(h.AuthLog.Entries, e =>
            e.Level == LogLevel.Information
            && e.Message.Contains("cerró sesión")
            && e.Message.Contains(sesion.Device.Id.ToString()));
    }

    [Fact]
    public async Task Logout_ConAllDevices_EliminaTodosLosDispositivos()
    {
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000001");
        await h.Auth.LoginAsync(Login(installationId: "telefono-1", fcmToken: "fcm-1"));
        await h.Auth.LoginAsync(Login(installationId: "telefono-2", fcmToken: "fcm-2"));

        await h.Auth.LogoutAsync(user.Id, new LogoutRequestDto(null, AllDevices: true));

        Assert.Empty(await h.ReloadDevicesOfUserAsync(user.Id));

        Assert.Contains(h.AuthLog.Entries, e =>
            e.Level == LogLevel.Information
            && e.Message.Contains("todos sus dispositivos")
            && e.Message.Contains("Dispositivos eliminados")
            && e.Message.Contains(": 1."));
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
