using FindMyFamily.Core.DTOs.Devices;
using FindMyFamily.Core.Exceptions;
using FindMyFamily.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FindMyFamily.Tests.Services;

/// <summary>
/// Alta del token FCM y desvinculación de dispositivos. Es la puerta de entrada del
/// Silent Push: sin un token correcto aquí, el flujo On-Demand no arranca.
/// </summary>
public class DeviceServiceTests
{
    [Fact]
    public async Task UpdateFcmToken_ActualizaElDispositivoIndicado()
    {
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000003");
        var device = await h.AddDeviceAsync(user.Id, fcmToken: "token-viejo");

        await h.Devices.UpdateFcmTokenAsync(user.Id, device.Id, new UpdateFcmTokenRequestDto("token-nuevo"));

        var actualizado = await h.ReloadDeviceAsync(device.Id);
        Assert.Equal("token-nuevo", actualizado!.FcmToken);
        Assert.True(actualizado.IsActive);
    }

    [Fact]
    public async Task UpdateFcmToken_NoPuedeModificarElDispositivoDeOtroUsuario()
    {
        await using var h = ServiceHarness.Create();
        var propietario = await h.AddUserAsync("0999000003");
        var atacante = await h.AddUserAsync("0999000004");
        var device = await h.AddDeviceAsync(propietario.Id, fcmToken: "token-de-la-victima");

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Devices.UpdateFcmTokenAsync(atacante.Id, device.Id,
                new UpdateFcmTokenRequestDto("token-del-atacante")));

        Assert.Equal("FORBIDDEN", ex.Code);

        var sinCambios = await h.ReloadDeviceAsync(device.Id);
        Assert.Equal("token-de-la-victima", sinCambios!.FcmToken);
    }

    [Fact]
    public async Task UpdateFcmToken_SinTokenLanza400()
    {
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000003");

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => h.Devices.UpdateFcmTokenAsync(user.Id, null, new UpdateFcmTokenRequestDto("   ")));

        Assert.Equal("INVALID_INPUT", ex.Code);
    }

    [Fact]
    public async Task UpdateFcmToken_SinDispositivoPrevioCreaElRegistro()
    {
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000003");

        await h.Devices.UpdateFcmTokenAsync(user.Id, null, new UpdateFcmTokenRequestDto("token-nuevo"));

        var device = Assert.Single(await h.ReloadDevicesOfUserAsync(user.Id));
        Assert.Equal("token-nuevo", device.FcmToken);
        Assert.True(device.IsActive);
    }

    [Fact]
    public async Task UnlinkDevice_ElPropietarioPuedeDesvincularlo()
    {
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000003");
        var device = await h.AddDeviceAsync(user.Id);

        await h.Devices.UnlinkDeviceAsync(user.Id, device.Id);

        h.Db.ChangeTracker.Clear();
        Assert.Null(await h.Db.Devices.FirstOrDefaultAsync(d => d.Id == device.Id));
    }

    [Fact]
    public async Task UnlinkDevice_UnTerceroNoPuede()
    {
        await using var h = ServiceHarness.Create();
        var propietario = await h.AddUserAsync("0999000003");
        var ajeno = await h.AddUserAsync("0999000004");
        var device = await h.AddDeviceAsync(propietario.Id);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Devices.UnlinkDeviceAsync(ajeno.Id, device.Id));

        Assert.Equal("FORBIDDEN", ex.Code);
    }

    [Fact]
    public async Task UnlinkDevice_ElSuperAdminPuedeDesvincularCualquiera()
    {
        await using var h = ServiceHarness.Create();
        var admin = await h.AddUserAsync("0999000001", role: "admin");
        var usuario = await h.AddUserAsync("0999000003");
        var device = await h.AddDeviceAsync(usuario.Id);

        await h.Devices.UnlinkDeviceAsync(admin.Id, device.Id);

        h.Db.ChangeTracker.Clear();
        Assert.Null(await h.Db.Devices.FirstOrDefaultAsync(d => d.Id == device.Id));
    }

    [Fact]
    public async Task UnlinkDevice_InexistenteLanza404()
    {
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000003");

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => h.Devices.UnlinkDeviceAsync(user.Id, Guid.NewGuid()));

        Assert.Equal("DEVICE_NOT_FOUND", ex.Code);
    }
}
