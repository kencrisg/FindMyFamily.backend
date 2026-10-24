using FindMyFamily.Core.DTOs.Users;
using FindMyFamily.Core.Exceptions;
using FindMyFamily.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FindMyFamily.Tests.Services;

public class UserServiceTests
{
    // -------------------------------------------------------------------------------
    // Perfil propio
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task UpdatePassword_RevocaTodasLasSesionesDelUsuario()
    {
        // Si el refresh token sobrevive al cambio de contraseña, quien tuviera la sesión
        // robada sigue dentro 90 días: el cambio de contraseña no sirve de nada.
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000001", password: "Vieja2026");
        await h.AddDeviceAsync(user.Id, refreshToken: "token-1", refreshTokenExpiry: DateTime.UtcNow.AddDays(30));
        await h.AddDeviceAsync(user.Id, refreshToken: "token-2", installationId: "otro",
            refreshTokenExpiry: DateTime.UtcNow.AddDays(30));

        await h.Users.UpdatePasswordAsync(user.Id, new UpdatePasswordRequestDto("Vieja2026", "Nueva2026"));

        var dispositivos = await h.ReloadDevicesOfUserAsync(user.Id);
        Assert.Equal(2, dispositivos.Count);
        Assert.All(dispositivos, d => Assert.Null(d.RefreshTokenHash));
        Assert.All(dispositivos, d => Assert.Null(d.RefreshTokenExpiry));
    }

    [Fact]
    public async Task UpdatePassword_ConLaActualIncorrectaLanza401()
    {
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000001", password: "Vieja2026");

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => h.Users.UpdatePasswordAsync(user.Id, new UpdatePasswordRequestDto("NoEsLaMia1", "Nueva2026")));

        Assert.Equal("INVALID_PASSWORD", ex.Code);
    }

    [Fact]
    public async Task UpdatePassword_AplicaLaPoliticaALaNueva()
    {
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000001", password: "Vieja2026");

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => h.Users.UpdatePasswordAsync(user.Id, new UpdatePasswordRequestDto("Vieja2026", "corta")));

        Assert.Equal("PASSWORD_TOO_SHORT", ex.Code);
    }

    [Fact]
    public async Task UpdatePhoneNumber_ExigeLaContrasenaActual()
    {
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000001", password: "Valida2026");

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => h.Users.UpdatePhoneNumberAsync(user.Id, new UpdatePhoneRequestDto("0999000055", "Incorrecta1")));

        Assert.Equal("INVALID_PASSWORD", ex.Code);
    }

    [Fact]
    public async Task UpdatePhoneNumber_NormalizaYRechazaDuplicados()
    {
        await using var h = ServiceHarness.Create();
        var user = await h.AddUserAsync("0999000001", password: "Valida2026");
        await h.AddUserAsync("0999000002");

        var perfil = await h.Users.UpdatePhoneNumberAsync(user.Id,
            new UpdatePhoneRequestDto("099-900-0055", "Valida2026"));
        Assert.Equal("0999000055", perfil.PhoneNumber);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => h.Users.UpdatePhoneNumberAsync(user.Id, new UpdatePhoneRequestDto("0999000002", "Valida2026")));
        Assert.Equal("PHONE_ALREADY_EXISTS", ex.Code);
    }

    // -------------------------------------------------------------------------------
    // Operaciones de Administrador de Sistema
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task AdminCreateUser_CreaElUsuarioYLoAsignaALaFamilia()
    {
        await using var h = ServiceHarness.Create();
        var admin = await h.AddUserAsync("0999000001", role: "admin");
        var familia = await h.AddFamilyAsync();

        var perfil = await h.Users.AdminCreateUserAsync(admin.Id, new AdminCreateUserRequestDto(
            "Maria", "Castro", "0999000002", "Maria2026", familia.Id, "admin"));

        Assert.Equal("user", perfil.SystemRole);
        var pertenencia = Assert.Single(perfil.Families);
        Assert.Equal("admin", pertenencia.FamilyRole);
    }

    [Fact]
    public async Task AdminCreateUser_NoPuedeCrearOtroSuperAdmin()
    {
        // La promoción a Administrador de Sistema no se expone por la API a propósito.
        await using var h = ServiceHarness.Create();
        var admin = await h.AddUserAsync("0999000001", role: "admin");
        var familia = await h.AddFamilyAsync();

        var perfil = await h.Users.AdminCreateUserAsync(admin.Id, new AdminCreateUserRequestDto(
            "Intruso", "X", "0999000002", "Valida2026", familia.Id, "admin"));

        Assert.Equal("user", perfil.SystemRole);
    }

    [Fact]
    public async Task AdminCreateUser_UnUsuarioNormalNoPuede()
    {
        await using var h = ServiceHarness.Create();
        var normal = await h.AddUserAsync("0999000002");
        var familia = await h.AddFamilyAsync();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Users.AdminCreateUserAsync(normal.Id, new AdminCreateUserRequestDto(
                "X", "Y", "0999000050", "Valida2026", familia.Id, "member")));

        Assert.Equal("SYSTEM_ADMIN_REQUIRED", ex.Code);
    }

    [Fact]
    public async Task AdminCreateUser_ConFamiliaInexistenteLanza404()
    {
        await using var h = ServiceHarness.Create();
        var admin = await h.AddUserAsync("0999000001", role: "admin");

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => h.Users.AdminCreateUserAsync(admin.Id, new AdminCreateUserRequestDto(
                "X", "Y", "0999000050", "Valida2026", Guid.NewGuid(), "member")));

        Assert.Equal("FAMILY_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task AdminDeleteUser_BorraLogicamenteYLimpiaPertenenciasYSesiones()
    {
        await using var h = ServiceHarness.Create();
        var admin = await h.AddUserAsync("0999000001", role: "admin");
        var familia = await h.AddFamilyAsync();
        var victima = await h.AddUserAsync("0999000002");
        await h.AddMemberAsync(familia.Id, victima.Id);
        await h.AddDeviceAsync(victima.Id, refreshToken: "token", refreshTokenExpiry: DateTime.UtcNow.AddDays(30));

        await h.Users.AdminDeleteUserAsync(admin.Id, victima.Id);

        var borrado = await h.ReloadUserIncludingDeletedAsync(victima.Id);
        Assert.NotNull(borrado);
        Assert.True(borrado!.IsDeleted);
        Assert.Empty(await h.Db.FamilyMembers.Where(fm => fm.UserId == victima.Id).ToListAsync());

        var dispositivo = Assert.Single(await h.ReloadDevicesOfUserAsync(victima.Id));
        Assert.False(dispositivo.IsActive);
        Assert.Null(dispositivo.RefreshTokenHash);
    }

    [Fact]
    public async Task AdminDeleteUser_NoPuedeBorrarseASiMismo()
    {
        await using var h = ServiceHarness.Create();
        var admin = await h.AddUserAsync("0999000001", role: "admin");

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => h.Users.AdminDeleteUserAsync(admin.Id, admin.Id));

        Assert.Equal("CANNOT_DELETE_SELF", ex.Code);
    }

    [Fact]
    public async Task AdminRestoreUser_DevuelveLaCuentaSinFamilia()
    {
        await using var h = ServiceHarness.Create();
        var admin = await h.AddUserAsync("0999000001", role: "admin");
        var familia = await h.AddFamilyAsync();
        var victima = await h.AddUserAsync("0999000002");
        await h.AddMemberAsync(familia.Id, victima.Id);
        await h.Users.AdminDeleteUserAsync(admin.Id, victima.Id);

        await h.Users.AdminRestoreUserAsync(admin.Id, victima.Id);

        // Se comprueba contra la base de datos y no contra el DTO devuelto: este test
        // comparte un único DbContext entre el borrado y la restauración, mientras que en
        // una petición real cada una tiene el suyo. Con el contexto compartido las
        // pertenencias borradas con ExecuteDelete siguen rastreadas en memoria.
        var restaurado = await h.ReloadUserIncludingDeletedAsync(victima.Id);
        Assert.NotNull(restaurado);
        Assert.False(restaurado!.IsDeleted);

        // La cuenta vuelve sin familia: restaurar no reinserta a nadie en ningún grupo.
        Assert.Empty(await h.Db.FamilyMembers.Where(fm => fm.UserId == victima.Id).ToListAsync());
    }

    [Fact]
    public async Task AdminRestoreUser_SobreUnaCuentaActivaLanzaConflicto()
    {
        await using var h = ServiceHarness.Create();
        var admin = await h.AddUserAsync("0999000001", role: "admin");
        var activo = await h.AddUserAsync("0999000002");

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => h.Users.AdminRestoreUserAsync(admin.Id, activo.Id));

        Assert.Equal("USER_NOT_DELETED", ex.Code);
    }

    [Fact]
    public async Task AdminGetDeletedUsers_SoloDevuelveLasBorradas()
    {
        await using var h = ServiceHarness.Create();
        var admin = await h.AddUserAsync("0999000001", role: "admin");
        await h.AddUserAsync("0999000002");
        await h.AddUserAsync("0999000003", deletedAt: DateTime.UtcNow);

        var papelera = await h.Users.AdminGetDeletedUsersAsync(admin.Id);

        Assert.Single(papelera);
        Assert.Equal("0999000003", papelera[0].PhoneNumber);
    }

    [Fact]
    public async Task AdminGetAllUsers_NoDevuelveLasCuentasBorradas()
    {
        await using var h = ServiceHarness.Create();
        var admin = await h.AddUserAsync("0999000001", role: "admin");
        await h.AddUserAsync("0999000002");
        await h.AddUserAsync("0999000003", deletedAt: DateTime.UtcNow);

        var usuarios = await h.Users.AdminGetAllUsersAsync(admin.Id);

        Assert.Equal(2, usuarios.Count);
        Assert.DoesNotContain(usuarios, u => u.PhoneNumber == "0999000003");
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-5, 50)]
    [InlineData(1, 999999)]
    [InlineData(1, 0)]
    public async Task AdminGetAllUsers_AcotaLaPaginacionEnLugarDeReventar(int page, int pageSize)
    {
        // page=0 generaba un OFFSET negativo y pageSize enorme volcaba la tabla entera.
        await using var h = ServiceHarness.Create();
        var admin = await h.AddUserAsync("0999000001", role: "admin");

        var usuarios = await h.Users.AdminGetAllUsersAsync(admin.Id, page, pageSize);

        Assert.NotNull(usuarios);
    }

    [Fact]
    public async Task AdminUpdateUser_ReasignaFamiliaYRolSinTocarElResto()
    {
        await using var h = ServiceHarness.Create();
        var admin = await h.AddUserAsync("0999000001", role: "admin");
        var familia = await h.AddFamilyAsync("Familia Nueva");
        var usuario = await h.AddUserAsync("0999000002", firstName: "Jorge", lastName: "Lopez");

        var perfil = await h.Users.AdminUpdateUserAsync(admin.Id, usuario.Id,
            new AdminUpdateUserRequestDto("Jorge Luis", null, null, familia.Id, "admin"));

        Assert.Equal("Jorge Luis", perfil.FirstName);
        Assert.Equal("Lopez", perfil.LastName);
        var pertenencia = Assert.Single(perfil.Families);
        Assert.Equal("admin", pertenencia.FamilyRole);
    }
}
