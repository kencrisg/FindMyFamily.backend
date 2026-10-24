using FindMyFamily.Core.DTOs.Families;
using FindMyFamily.Core.Exceptions;
using FindMyFamily.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FindMyFamily.Tests.Services;

/// <summary>
/// Matriz de permisos del grupo familiar y semántica de eliminación.
/// Es la zona de mayor riesgo del backend: un fallo aquí expone la ubicación de una
/// persona a quien no debe verla, o deja una cuenta activa que debería estar cerrada.
/// </summary>
public class FamilyServiceTests
{
    // -------------------------------------------------------------------------------
    // Crear familias: exclusivo del Administrador de Sistema
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task CreateFamily_ElSuperAdminPuede()
    {
        await using var h = ServiceHarness.Create();
        var admin = await h.AddUserAsync("0999000001", role: "admin");

        var familia = await h.Families.CreateFamilyAsync(admin.Id, new CreateFamilyRequestDto("Familia Castro"));

        Assert.Equal("Familia Castro", familia.Name);
        Assert.Equal(0, familia.MembersCount);
    }

    [Fact]
    public async Task CreateFamily_UnAdminDeFamiliaNoPuede()
    {
        await using var h = ServiceHarness.Create();
        var familia = await h.AddFamilyAsync();
        var adminFamiliar = await h.AddUserAsync("0999000002");
        await h.AddMemberAsync(familia.Id, adminFamiliar.Id, "admin");

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Families.CreateFamilyAsync(adminFamiliar.Id, new CreateFamilyRequestDto("Familia Pirata")));

        Assert.Equal("SYSTEM_ADMIN_REQUIRED", ex.Code);
    }

    [Fact]
    public async Task UpdateYDeleteFamily_SoloElSuperAdmin()
    {
        await using var h = ServiceHarness.Create();
        var familia = await h.AddFamilyAsync();
        var adminFamiliar = await h.AddUserAsync("0999000002");
        await h.AddMemberAsync(familia.Id, adminFamiliar.Id, "admin");

        await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Families.UpdateFamilyAsync(adminFamiliar.Id, familia.Id, new UpdateFamilyRequestDto("Otra")));
        await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Families.DeleteFamilyAsync(adminFamiliar.Id, familia.Id));
    }

    // -------------------------------------------------------------------------------
    // Visibilidad de la familia
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task GetFamilyById_UnMiembroVeSuFamilia()
    {
        await using var h = ServiceHarness.Create();
        var familia = await h.AddFamilyAsync("Familia Castro");
        var miembro = await h.AddUserAsync("0999000003", firstName: "Luis");
        await h.AddMemberAsync(familia.Id, miembro.Id);

        var detalle = await h.Families.GetFamilyByIdAsync(miembro.Id, familia.Id);

        Assert.Equal("Familia Castro", detalle.Name);
        Assert.Single(detalle.Members);
        Assert.Equal("Luis", detalle.Members[0].FirstName);
    }

    [Fact]
    public async Task GetFamilyById_UnExtranoNoVeUnaFamiliaAjena()
    {
        await using var h = ServiceHarness.Create();
        var familia = await h.AddFamilyAsync();
        var extrano = await h.AddUserAsync("0999000099");

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Families.GetFamilyByIdAsync(extrano.Id, familia.Id));

        Assert.Equal("FORBIDDEN", ex.Code);
    }

    [Fact]
    public async Task GetFamilyById_ElSuperAdminVeCualquierFamilia()
    {
        await using var h = ServiceHarness.Create();
        var familia = await h.AddFamilyAsync();
        var admin = await h.AddUserAsync("0999000001", role: "admin");

        var detalle = await h.Families.GetFamilyByIdAsync(admin.Id, familia.Id);

        Assert.Equal(familia.Id, detalle.Id);
    }

    // -------------------------------------------------------------------------------
    // CRUD de miembros por el Admin de Familia
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task CreateMemberUser_ElAdminDeFamiliaCreaLaCuentaYLaIncorpora()
    {
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);

        var detalle = await h.Families.CreateMemberUserAsync(adminFamiliar, familia,
            new CreateFamilyMemberUserRequestDto("Luis", "Castro", "0999000003", "Luis2026"));

        Assert.Equal(2, detalle.Members.Count);
        var nuevo = Assert.Single(detalle.Members, m => m.PhoneNumber == "0999000003");
        Assert.Equal("member", nuevo.Role);

        // La cuenta creada es un usuario normal del sistema, nunca un superadmin.
        var creado = await h.Db.Users.SingleAsync(u => u.PhoneNumber == "0999000003");
        Assert.Equal("user", creado.Role);
    }

    [Fact]
    public async Task CreateMemberUser_UnMiembroRasoNoPuede()
    {
        await using var h = ServiceHarness.Create();
        var familia = await h.AddFamilyAsync();
        var miembro = await h.AddUserAsync("0999000003");
        await h.AddMemberAsync(familia.Id, miembro.Id, "member");

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Families.CreateMemberUserAsync(miembro.Id, familia.Id,
                new CreateFamilyMemberUserRequestDto("X", "Y", "0999000050", "Valida2026")));

        Assert.Equal("ADMIN_REQUIRED", ex.Code);
    }

    [Fact]
    public async Task CreateMemberUser_AplicaLaPoliticaDeContrasenas()
    {
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => h.Families.CreateMemberUserAsync(adminFamiliar, familia,
                new CreateFamilyMemberUserRequestDto("X", "Y", "0999000050", "123")));

        Assert.Equal("PASSWORD_TOO_SHORT", ex.Code);
    }

    [Fact]
    public async Task CreateMemberUser_ConTelefonoYaRegistradoLanzaConflicto()
    {
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);
        await h.AddUserAsync("0999000050");

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => h.Families.CreateMemberUserAsync(adminFamiliar, familia,
                new CreateFamilyMemberUserRequestDto("X", "Y", "0999000050", "Valida2026")));

        Assert.Equal("PHONE_ALREADY_EXISTS", ex.Code);
    }

    [Fact]
    public async Task UpdateMemberUser_ElAdminDeFamiliaEditaAUnMiembro()
    {
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);
        var miembro = await h.AddUserAsync("0999000003", firstName: "Luis");
        await h.AddMemberAsync(familia, miembro.Id);

        var detalle = await h.Families.UpdateMemberUserAsync(adminFamiliar, familia, miembro.Id,
            new UpdateFamilyMemberUserRequestDto("Luis Alberto", null, "0999000004"));

        var editado = Assert.Single(detalle.Members, m => m.UserId == miembro.Id);
        Assert.Equal("Luis Alberto", editado.FirstName);
        Assert.Equal("0999000004", editado.PhoneNumber);
        // Los campos no enviados se conservan.
        Assert.Equal("Apellido", editado.LastName);
    }

    [Fact]
    public async Task UpdateMemberUser_NoPuedeTocarAUnSuperAdminDeSuFamilia()
    {
        // Sería una escalada de privilegios: cambiarle el teléfono a un superadmin
        // permitiría luego apropiarse de esa cuenta.
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);
        var superAdmin = await h.AddUserAsync("0999000001", role: "admin");
        await h.AddMemberAsync(familia, superAdmin.Id);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Families.UpdateMemberUserAsync(adminFamiliar, familia, superAdmin.Id,
                new UpdateFamilyMemberUserRequestDto("Hackeado", null, null)));

        Assert.Equal("CANNOT_MANAGE_SYSTEM_ADMIN", ex.Code);
    }

    [Fact]
    public async Task UpdateMemberUser_SobreAlguienQueNoEsMiembroLanza404()
    {
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);
        var ajeno = await h.AddUserAsync("0999000077");

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => h.Families.UpdateMemberUserAsync(adminFamiliar, familia, ajeno.Id,
                new UpdateFamilyMemberUserRequestDto("X", null, null)));

        Assert.Equal("MEMBER_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task ResetMemberPassword_CambiaLaClaveYRevocaLasSesionesDelMiembro()
    {
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);
        var miembro = await h.AddUserAsync("0999000003", password: "Vieja2026");
        await h.AddMemberAsync(familia, miembro.Id);
        await h.AddDeviceAsync(miembro.Id, refreshToken: "token-activo",
            refreshTokenExpiry: DateTime.UtcNow.AddDays(30));

        await h.Families.ResetMemberPasswordAsync(adminFamiliar, familia, miembro.Id,
            new ResetMemberPasswordRequestDto("Nueva2026"));

        var actualizado = await h.Db.Users.SingleAsync(u => u.Id == miembro.Id);
        Assert.True(h.PasswordHasher.VerifyPassword("Nueva2026", actualizado.PasswordHash));

        // No exige la contraseña anterior, así que dejar sesiones vivas sería un agujero.
        var dispositivos = await h.ReloadDevicesOfUserAsync(miembro.Id);
        Assert.All(dispositivos, d => Assert.Null(d.RefreshTokenHash));
    }

    [Fact]
    public async Task ResetMemberPassword_AplicaLaPoliticaDeContrasenas()
    {
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);
        var miembro = await h.AddUserAsync("0999000003");
        await h.AddMemberAsync(familia, miembro.Id);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => h.Families.ResetMemberPasswordAsync(adminFamiliar, familia, miembro.Id,
                new ResetMemberPasswordRequestDto("solotexto")));

        Assert.Equal("PASSWORD_TOO_WEAK", ex.Code);
    }

    // -------------------------------------------------------------------------------
    // Roles dentro de la familia
    // -------------------------------------------------------------------------------

    [Theory]
    [InlineData("admin")]
    [InlineData("member")]
    public async Task UpdateMemberRole_AdmiteSoloLosRolesValidos(string rol)
    {
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);
        var miembro = await h.AddUserAsync("0999000003");
        await h.AddMemberAsync(familia, miembro.Id);

        await h.Families.UpdateMemberRoleAsync(adminFamiliar, familia, miembro.Id,
            new UpdateMemberRoleRequestDto(rol));

        var actualizado = await h.Db.FamilyMembers.SingleAsync(fm => fm.UserId == miembro.Id);
        Assert.Equal(rol, actualizado.Role);
    }

    [Theory]
    [InlineData("superadmin")]
    [InlineData("")]
    [InlineData("owner")]
    public async Task UpdateMemberRole_RechazaRolesInventados(string rol)
    {
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);
        var miembro = await h.AddUserAsync("0999000003");
        await h.AddMemberAsync(familia, miembro.Id);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => h.Families.UpdateMemberRoleAsync(adminFamiliar, familia, miembro.Id,
                new UpdateMemberRoleRequestDto(rol)));

        Assert.Equal("INVALID_ROLE", ex.Code);
    }

    [Fact]
    public async Task UpdateMemberRole_NoPuedeDegradarAlUnicoAdministrador()
    {
        // Dejaría la familia sin nadie capaz de gestionarla.
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => h.Families.UpdateMemberRoleAsync(adminFamiliar, familia, adminFamiliar,
                new UpdateMemberRoleRequestDto("member")));

        Assert.Equal("LAST_FAMILY_ADMIN", ex.Code);
    }

    [Fact]
    public async Task UpdateMemberRole_PuedeDegradarSiQuedaOtroAdministrador()
    {
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);
        var otroAdmin = await h.AddUserAsync("0999000004");
        await h.AddMemberAsync(familia, otroAdmin.Id, "admin");

        await h.Families.UpdateMemberRoleAsync(adminFamiliar, familia, otroAdmin.Id,
            new UpdateMemberRoleRequestDto("member"));

        var actualizado = await h.Db.FamilyMembers.SingleAsync(fm => fm.UserId == otroAdmin.Id);
        Assert.Equal("member", actualizado.Role);
    }

    // -------------------------------------------------------------------------------
    // Vinculación de usuarios existentes: restringida al Super Admin
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task AddMemberByPhone_UnAdminDeFamiliaNoPuedeVincularCuentasAjenas()
    {
        // Si pudiera, bastaría conocer un número para meter a su dueño en la familia y
        // localizarlo sin que se enterara, porque el push es silencioso.
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);
        await h.AddUserAsync("0999000077");

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Families.AddMemberByPhoneAsync(adminFamiliar, familia,
                new AddFamilyMemberRequestDto("0999000077")));

        Assert.Equal("SYSTEM_ADMIN_REQUIRED", ex.Code);
    }

    [Fact]
    public async Task AddMemberByPhone_ElSuperAdminSiPuede()
    {
        await using var h = ServiceHarness.Create();
        var admin = await h.AddUserAsync("0999000001", role: "admin");
        var familia = await h.AddFamilyAsync();
        await h.AddUserAsync("0999000077");

        var detalle = await h.Families.AddMemberByPhoneAsync(admin.Id, familia.Id,
            new AddFamilyMemberRequestDto("0999000077"));

        Assert.Single(detalle.Members, m => m.PhoneNumber == "0999000077");
    }

    [Fact]
    public async Task AddMemberByPhone_ConFamiliaInexistenteLanza404()
    {
        await using var h = ServiceHarness.Create();
        var admin = await h.AddUserAsync("0999000001", role: "admin");
        await h.AddUserAsync("0999000077");

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => h.Families.AddMemberByPhoneAsync(admin.Id, Guid.NewGuid(),
                new AddFamilyMemberRequestDto("0999000077")));

        Assert.Equal("FAMILY_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task AddMemberByPhone_SiYaEsMiembroLanzaConflicto()
    {
        await using var h = ServiceHarness.Create();
        var admin = await h.AddUserAsync("0999000001", role: "admin");
        var familia = await h.AddFamilyAsync();
        var usuario = await h.AddUserAsync("0999000077");
        await h.AddMemberAsync(familia.Id, usuario.Id);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => h.Families.AddMemberByPhoneAsync(admin.Id, familia.Id,
                new AddFamilyMemberRequestDto("0999000077")));

        Assert.Equal("ALREADY_MEMBER", ex.Code);
    }

    // -------------------------------------------------------------------------------
    // Semántica de eliminación (borrado lógico)
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task RemoveMember_SiEraSuUnicaFamilia_BorraLaCuentaLogicamente()
    {
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);
        var miembro = await h.AddUserAsync("0999000003");
        await h.AddMemberAsync(familia, miembro.Id);
        await h.AddDeviceAsync(miembro.Id, refreshToken: "token",
            refreshTokenExpiry: DateTime.UtcNow.AddDays(30));

        await h.Families.RemoveMemberAsync(adminFamiliar, familia, miembro.Id);

        // La fila se conserva (histórico de ubicaciones y auditoría) pero marcada.
        var borrado = await h.ReloadUserIncludingDeletedAsync(miembro.Id);
        Assert.NotNull(borrado);
        Assert.True(borrado!.IsDeleted);

        // Y deja de existir para el resto del sistema.
        Assert.Null(await h.Db.Users.FirstOrDefaultAsync(u => u.Id == miembro.Id));

        // Sin pertenencias y sin sesiones que sobrevivan.
        Assert.Empty(await h.Db.FamilyMembers.Where(fm => fm.UserId == miembro.Id).ToListAsync());
        var dispositivo = Assert.Single(await h.ReloadDevicesOfUserAsync(miembro.Id));
        Assert.False(dispositivo.IsActive);
        Assert.Null(dispositivo.RefreshTokenHash);
    }

    [Fact]
    public async Task RemoveMember_SiPerteneceAOtraFamilia_SoloLoDesvincula()
    {
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);
        var otraFamilia = await h.AddFamilyAsync("Familia Abuelos");
        var miembro = await h.AddUserAsync("0999000003");
        await h.AddMemberAsync(familia, miembro.Id);
        await h.AddMemberAsync(otraFamilia.Id, miembro.Id);

        await h.Families.RemoveMemberAsync(adminFamiliar, familia, miembro.Id);

        var sigueActivo = await h.Db.Users.FirstOrDefaultAsync(u => u.Id == miembro.Id);
        Assert.NotNull(sigueActivo);
        Assert.False(sigueActivo!.IsDeleted);

        var pertenencias = await h.Db.FamilyMembers.Where(fm => fm.UserId == miembro.Id).ToListAsync();
        Assert.Single(pertenencias);
        Assert.Equal(otraFamilia.Id, pertenencias[0].FamilyId);
    }

    [Fact]
    public async Task RemoveMember_SalidaVoluntaria_NoBorraLaCuenta()
    {
        await using var h = ServiceHarness.Create();
        var familia = await h.AddFamilyAsync();
        var miembro = await h.AddUserAsync("0999000003");
        await h.AddMemberAsync(familia.Id, miembro.Id, "member");

        await h.Families.RemoveMemberAsync(miembro.Id, familia.Id, miembro.Id);

        var sigueActivo = await h.Db.Users.FirstOrDefaultAsync(u => u.Id == miembro.Id);
        Assert.NotNull(sigueActivo);
        Assert.False(sigueActivo!.IsDeleted);
        Assert.Empty(await h.Db.FamilyMembers.Where(fm => fm.UserId == miembro.Id).ToListAsync());
    }

    [Fact]
    public async Task RemoveMember_UnMiembroRasoNoPuedeEcharAOtro()
    {
        await using var h = ServiceHarness.Create();
        var familia = await h.AddFamilyAsync();
        var uno = await h.AddUserAsync("0999000003");
        var otro = await h.AddUserAsync("0999000004");
        await h.AddMemberAsync(familia.Id, uno.Id, "member");
        await h.AddMemberAsync(familia.Id, otro.Id, "member");

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Families.RemoveMemberAsync(uno.Id, familia.Id, otro.Id));

        Assert.Equal("ADMIN_REQUIRED", ex.Code);
    }

    [Fact]
    public async Task RemoveMember_NoPuedeEcharAlUnicoAdministrador()
    {
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);
        var otroAdmin = await h.AddUserAsync("0999000004");
        await h.AddMemberAsync(familia, otroAdmin.Id, "admin");

        // Con dos admins se puede.
        await h.Families.RemoveMemberAsync(adminFamiliar, familia, otroAdmin.Id);

        // Al quedar uno solo, ya no.
        var miembro = await h.AddUserAsync("0999000005");
        await h.AddMemberAsync(familia, miembro.Id);
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => h.Families.RemoveMemberAsync(adminFamiliar, familia, adminFamiliar));

        Assert.Equal("LAST_FAMILY_ADMIN", ex.Code);
    }

    [Fact]
    public async Task RemoveMember_UnTelefonoDeCuentaBorradaDaUnConflictoEntendible()
    {
        // El índice único del teléfono es global, así que una cuenta borrada sigue
        // ocupando su número: hay que explicarlo y no dejar que estalle la restricción.
        await using var h = ServiceHarness.Create();
        var (_, familia, adminFamiliar) = await EscenarioConAdminFamiliar(h);
        var miembro = await h.AddUserAsync("0999000003");
        await h.AddMemberAsync(familia, miembro.Id);

        await h.Families.RemoveMemberAsync(adminFamiliar, familia, miembro.Id);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => h.Families.CreateMemberUserAsync(adminFamiliar, familia,
                new CreateFamilyMemberUserRequestDto("Otro", "X", "0999000003", "Valida2026")));

        Assert.Equal("PHONE_BELONGS_TO_DELETED_USER", ex.Code);
    }

    [Fact]
    public async Task GetFamilyById_NoMuestraFilasFantasmaDeCuentasBorradas()
    {
        await using var h = ServiceHarness.Create();
        var familia = await h.AddFamilyAsync();
        var activo = await h.AddUserAsync("0999000003", firstName: "Luis");
        var borrado = await h.AddUserAsync("0999000004", firstName: "Fantasma", deletedAt: DateTime.UtcNow);
        await h.AddMemberAsync(familia.Id, activo.Id, "admin");
        // Pertenencia residual de una cuenta borrada (no debería existir, pero si existe
        // no puede traducirse en un miembro con los campos vacíos).
        await h.AddMemberAsync(familia.Id, borrado.Id);

        var detalle = await h.Families.GetFamilyByIdAsync(activo.Id, familia.Id);

        Assert.Single(detalle.Members);
        Assert.Equal("Luis", detalle.Members[0].FirstName);
    }

    // -------------------------------------------------------------------------------

    /// <summary>Familia con un Admin de Familia. Devuelve (superAdminId, familiaId, adminFamiliarId).</summary>
    private static async Task<(Guid superAdmin, Guid familia, Guid adminFamiliar)> EscenarioConAdminFamiliar(ServiceHarness h)
    {
        var superAdmin = await h.AddUserAsync("0999000000", role: "admin");
        var familia = await h.AddFamilyAsync("Familia Castro");
        var adminFamiliar = await h.AddUserAsync("0999000002", firstName: "Maria");
        await h.AddMemberAsync(familia.Id, adminFamiliar.Id, "admin");

        return (superAdmin.Id, familia.Id, adminFamiliar.Id);
    }
}
