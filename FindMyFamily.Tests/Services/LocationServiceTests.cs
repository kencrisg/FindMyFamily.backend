using FindMyFamily.Core.DTOs.Locations;
using FindMyFamily.Core.Exceptions;
using FindMyFamily.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FindMyFamily.Tests.Services;

/// <summary>
/// El flujo On-Demand: quién puede pedir la ubicación de quién, qué se responde cuando el
/// familiar es inalcanzable, y a quién se difunde la coordenada reportada.
/// </summary>
public class LocationServiceTests
{
    // -------------------------------------------------------------------------------
    // Permisos de la petición
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task RequestLocation_SiNoPertenecesALaFamiliaLanza403()
    {
        await using var h = ServiceHarness.Create();
        var familia = await h.AddFamilyAsync();
        var objetivo = await h.AddUserAsync("0999000003");
        await h.AddMemberAsync(familia.Id, objetivo.Id);
        var extrano = await h.AddUserAsync("0999000099");

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Locations.RequestLocationAsync(extrano.Id, new RequestLocationDto(objetivo.Id, familia.Id)));

        Assert.Equal("FORBIDDEN", ex.Code);
    }

    [Fact]
    public async Task RequestLocation_SiElObjetivoNoEsDeLaFamiliaLanza403()
    {
        await using var h = ServiceHarness.Create();
        var familia = await h.AddFamilyAsync();
        var solicitante = await h.AddUserAsync("0999000002");
        await h.AddMemberAsync(familia.Id, solicitante.Id);
        var ajeno = await h.AddUserAsync("0999000077");

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Locations.RequestLocationAsync(solicitante.Id, new RequestLocationDto(ajeno.Id, familia.Id)));

        Assert.Equal("FORBIDDEN", ex.Code);
    }

    [Fact]
    public async Task RequestLocation_ConIdentificadoresVaciosLanza400()
    {
        await using var h = ServiceHarness.Create();
        var usuario = await h.AddUserAsync("0999000002");

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => h.Locations.RequestLocationAsync(usuario.Id, new RequestLocationDto(Guid.Empty, Guid.Empty)));

        Assert.Equal("INVALID_INPUT", ex.Code);
    }

    // -------------------------------------------------------------------------------
    // Camino feliz y degradación cuando el familiar es inalcanzable
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task RequestLocation_ConDispositivoActivo_EnviaElSilentPush()
    {
        await using var h = ServiceHarness.Create();
        var (familia, madre, padre) = await EscenarioFamiliar(h);
        await h.AddDeviceAsync(padre, fcmToken: "fcm-del-padre");

        var respuesta = await h.Locations.RequestLocationAsync(madre, new RequestLocationDto(padre, familia));

        Assert.Equal(LocationRequestStatus.Requested, respuesta.Status);
        Assert.NotNull(respuesta.RequestId);

        var push = Assert.Single(h.Push.SentMessages);
        Assert.Equal("fcm-del-padre", push.FcmToken);
        Assert.Equal(respuesta.RequestId, push.RequestId);
        Assert.Equal(madre, push.RequesterId);

        // La petición queda registrada: es la auditoría de quién pidió qué, y es lo que
        // autoriza y acota el reporte posterior.
        var registrada = await h.Db.LocationRequests.SingleAsync();
        Assert.Equal(madre, registrada.RequesterId);
        Assert.Equal(padre, registrada.TargetUserId);
        Assert.Null(registrada.FulfilledAt);
    }

    [Fact]
    public async Task RequestLocation_SinDispositivo_DevuelveUnavailableYNoLanzaError()
    {
        // Esto antes era un 404, así que la app no tenía con qué construir el mensaje de
        // "no disponible" ni la hora de la última ubicación.
        await using var h = ServiceHarness.Create();
        var (familia, madre, padre) = await EscenarioFamiliar(h);

        var respuesta = await h.Locations.RequestLocationAsync(madre, new RequestLocationDto(padre, familia));

        Assert.Equal(LocationRequestStatus.Unavailable, respuesta.Status);
        Assert.Null(respuesta.RequestId);
        Assert.Null(respuesta.LastKnownLocation);
        Assert.Empty(h.Push.SentMessages);
    }

    [Fact]
    public async Task RequestLocation_SinTokenFcm_DevuelveUnavailable()
    {
        await using var h = ServiceHarness.Create();
        var (familia, madre, padre) = await EscenarioFamiliar(h);
        await h.AddDeviceAsync(padre, fcmToken: null);

        var respuesta = await h.Locations.RequestLocationAsync(madre, new RequestLocationDto(padre, familia));

        Assert.Equal(LocationRequestStatus.Unavailable, respuesta.Status);
    }

    [Fact]
    public async Task RequestLocation_ConDispositivoInactivo_DevuelveUnavailable()
    {
        await using var h = ServiceHarness.Create();
        var (familia, madre, padre) = await EscenarioFamiliar(h);
        await h.AddDeviceAsync(padre, isActive: false);

        var respuesta = await h.Locations.RequestLocationAsync(madre, new RequestLocationDto(padre, familia));

        Assert.Equal(LocationRequestStatus.Unavailable, respuesta.Status);
    }

    [Fact]
    public async Task RequestLocation_DevuelveLaUltimaUbicacionConocidaConSuHora()
    {
        // Es exactamente lo que la app necesita para "no disponible, su última ubicación
        // fue a las …".
        await using var h = ServiceHarness.Create();
        var (familia, madre, padre) = await EscenarioFamiliar(h);
        var momento = DateTime.UtcNow.AddMinutes(-45);
        await h.AddLocationAsync(padre, -0.180653m, -78.467834m, momento);

        var respuesta = await h.Locations.RequestLocationAsync(madre, new RequestLocationDto(padre, familia));

        Assert.Equal(LocationRequestStatus.Unavailable, respuesta.Status);
        Assert.NotNull(respuesta.LastKnownLocation);
        Assert.Equal(-0.180653m, respuesta.LastKnownLocation!.Latitude);
        Assert.Equal(-78.467834m, respuesta.LastKnownLocation.Longitude);
        Assert.Equal(momento, respuesta.LastKnownLocation.CapturedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RequestLocation_DevuelveLaMasRecienteDeVariasUbicaciones()
    {
        await using var h = ServiceHarness.Create();
        var (familia, madre, padre) = await EscenarioFamiliar(h);
        await h.AddLocationAsync(padre, 1m, 1m, DateTime.UtcNow.AddHours(-3));
        await h.AddLocationAsync(padre, 2m, 2m, DateTime.UtcNow.AddMinutes(-10));
        await h.AddLocationAsync(padre, 3m, 3m, DateTime.UtcNow.AddHours(-1));

        var respuesta = await h.Locations.RequestLocationAsync(madre, new RequestLocationDto(padre, familia));

        Assert.Equal(2m, respuesta.LastKnownLocation!.Latitude);
    }

    [Fact]
    public async Task RequestLocation_SiFallaElEnvio_DevuelveUnavailableConLaUltimaConocida()
    {
        await using var h = ServiceHarness.Create();
        var (familia, madre, padre) = await EscenarioFamiliar(h);
        await h.AddDeviceAsync(padre);
        await h.AddLocationAsync(padre, 5m, 5m, DateTime.UtcNow.AddMinutes(-5));
        h.Push.ShouldSucceed = false;

        var respuesta = await h.Locations.RequestLocationAsync(madre, new RequestLocationDto(padre, familia));

        Assert.Equal(LocationRequestStatus.Unavailable, respuesta.Status);
        Assert.NotNull(respuesta.LastKnownLocation);
        Assert.Equal(5m, respuesta.LastKnownLocation!.Latitude);
    }

    [Fact]
    public async Task RequestLocation_LimitaLaFrecuenciaSobreElMismoFamiliar()
    {
        // Sin el tope, un miembro podía mantener encendido el GPS de otro en bucle:
        // justo lo que el modelo On-Demand existe para evitar.
        await using var h = ServiceHarness.Create();
        var (familia, madre, padre) = await EscenarioFamiliar(h);
        await h.AddDeviceAsync(padre);

        for (var i = 0; i < 10; i++)
        {
            await h.Locations.RequestLocationAsync(madre, new RequestLocationDto(padre, familia));
        }

        var ex = await Assert.ThrowsAsync<AppTooManyRequestsException>(
            () => h.Locations.RequestLocationAsync(madre, new RequestLocationDto(padre, familia)));

        Assert.Equal("LOCATION_REQUEST_RATE_LIMITED", ex.Code);
    }

    // -------------------------------------------------------------------------------
    // Reporte de coordenadas
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task ReportLocation_PersisteYNormalizaLaMarcaTemporal()
    {
        await using var h = ServiceHarness.Create();
        var usuario = await h.AddUserAsync("0999000003");

        var resultado = await h.Locations.ReportLocationAsync(usuario.Id,
            new ReportLocationDto(-0.180653m, -78.467834m));

        var guardada = await h.Db.Locations.SingleAsync();
        Assert.Equal(resultado.Id, guardada.Id);
        Assert.Equal(usuario.Id, guardada.UserId);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(10.0, null)]
    [InlineData(null, 10.0)]
    public async Task ReportLocation_SinCoordenadasLanza400(double? lat, double? lon)
    {
        // Siendo decimal no anulable, omitirlas daba 0,0 ("Null Island") guardado como
        // una ubicación válida en lugar de un error.
        await using var h = ServiceHarness.Create();
        var usuario = await h.AddUserAsync("0999000003");

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => h.Locations.ReportLocationAsync(usuario.Id,
                new ReportLocationDto((decimal?)lat, (decimal?)lon)));

        Assert.Equal("INVALID_COORDINATES", ex.Code);
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public async Task ReportLocation_ConCoordenadasFueraDeRangoLanza400(double lat, double lon)
    {
        await using var h = ServiceHarness.Create();
        var usuario = await h.AddUserAsync("0999000003");

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => h.Locations.ReportLocationAsync(usuario.Id,
                new ReportLocationDto((decimal)lat, (decimal)lon)));

        Assert.Equal("INVALID_COORDINATES", ex.Code);
    }

    [Fact]
    public async Task ReportLocation_RechazaUnaMarcaTemporalEnElFuturo()
    {
        // Una fecha futura quedaba fijada como "última ubicación" de forma permanente,
        // porque la consulta ordena por captured_at descendente.
        await using var h = ServiceHarness.Create();
        var usuario = await h.AddUserAsync("0999000003");

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => h.Locations.ReportLocationAsync(usuario.Id,
                new ReportLocationDto(1m, 1m, DateTime.UtcNow.AddHours(2))));

        Assert.Equal("INVALID_CAPTURED_AT", ex.Code);
    }

    [Fact]
    public async Task ReportLocation_RechazaUnaMarcaTemporalDemasiadoAntigua()
    {
        await using var h = ServiceHarness.Create();
        var usuario = await h.AddUserAsync("0999000003");

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => h.Locations.ReportLocationAsync(usuario.Id,
                new ReportLocationDto(1m, 1m, DateTime.UtcNow.AddDays(-2))));

        Assert.Equal("INVALID_CAPTURED_AT", ex.Code);
    }

    [Fact]
    public async Task ReportLocation_AceptaUnaMarcaTemporalSinZonaHorariaComoUtc()
    {
        // Un ISO sin 'Z' llega con Kind=Unspecified y Npgsql lo rechaza contra una columna
        // timestamptz: era un 500.
        await using var h = ServiceHarness.Create();
        var usuario = await h.AddUserAsync("0999000003");
        var sinZona = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-1), DateTimeKind.Unspecified);

        var resultado = await h.Locations.ReportLocationAsync(usuario.Id,
            new ReportLocationDto(1m, 1m, sinZona));

        Assert.Equal(DateTimeKind.Utc, resultado.CapturedAt.Kind);
    }

    // -------------------------------------------------------------------------------
    // Difusión: a quién llega la coordenada (el punto más sensible)
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task ReportLocation_SinPeticionPendiente_SoloSeDifundeAUnoMismo()
    {
        await using var h = ServiceHarness.Create();
        var (familia, madre, padre) = await EscenarioFamiliar(h);

        await h.Locations.ReportLocationAsync(padre, new ReportLocationDto(1m, 1m));

        var difusion = Assert.Single(h.Notifier.Broadcasts);
        var destinatario = Assert.Single(difusion.RecipientUserIds);
        Assert.Equal(padre, destinatario);
        Assert.NotEqual(madre, destinatario);
    }

    [Fact]
    public async Task ReportLocation_ConPeticionPendiente_SeDifundeAlSolicitante()
    {
        await using var h = ServiceHarness.Create();
        var (familia, madre, padre) = await EscenarioFamiliar(h);
        await h.AddPendingRequestAsync(madre, padre, familia);

        await h.Locations.ReportLocationAsync(padre, new ReportLocationDto(1m, 1m));

        var difusion = Assert.Single(h.Notifier.Broadcasts);
        Assert.Contains(madre, difusion.RecipientUserIds);
        Assert.Contains(padre, difusion.RecipientUserIds);
    }

    [Fact]
    public async Task ReportLocation_NoSeFiltraAOtraFamiliaDelMismoUsuario()
    {
        // La fuga que había: se difundía a TODAS las familias del usuario, así que una
        // petición hecha en un círculo entregaba la coordenada a los demás círculos.
        await using var h = ServiceHarness.Create();
        var (familiaMaterna, madre, padre) = await EscenarioFamiliar(h);

        var familiaPaterna = await h.AddFamilyAsync("Familia Paterna");
        var suegra = await h.AddUserAsync("0999000088");
        await h.AddMemberAsync(familiaPaterna.Id, suegra.Id);
        await h.AddMemberAsync(familiaPaterna.Id, padre);

        // Solo la madre pidió la ubicación.
        await h.AddPendingRequestAsync(madre, padre, familiaMaterna);

        await h.Locations.ReportLocationAsync(padre, new ReportLocationDto(1m, 1m));

        var difusion = Assert.Single(h.Notifier.Broadcasts);
        Assert.Contains(madre, difusion.RecipientUserIds);
        Assert.DoesNotContain(suegra.Id, difusion.RecipientUserIds);
    }

    [Fact]
    public async Task ReportLocation_NoSeDifundeAQuienYaFueExpulsadoDeLaFamilia()
    {
        // Entre la petición y la respuesta el solicitante puede haber perdido el acceso.
        await using var h = ServiceHarness.Create();
        var (familia, madre, padre) = await EscenarioFamiliar(h);
        await h.AddPendingRequestAsync(madre, padre, familia);

        var pertenencia = await h.Db.FamilyMembers.SingleAsync(fm => fm.UserId == madre);
        h.Db.FamilyMembers.Remove(pertenencia);
        await h.Db.SaveChangesAsync();
        h.Db.ChangeTracker.Clear();

        await h.Locations.ReportLocationAsync(padre, new ReportLocationDto(1m, 1m));

        var difusion = Assert.Single(h.Notifier.Broadcasts);
        Assert.DoesNotContain(madre, difusion.RecipientUserIds);
    }

    [Fact]
    public async Task ReportLocation_IgnoraPeticionesCaducadas()
    {
        await using var h = ServiceHarness.Create();
        var (familia, madre, padre) = await EscenarioFamiliar(h);
        await h.AddPendingRequestAsync(madre, padre, familia, expiresAt: DateTime.UtcNow.AddMinutes(-1));

        await h.Locations.ReportLocationAsync(padre, new ReportLocationDto(1m, 1m));

        var difusion = Assert.Single(h.Notifier.Broadcasts);
        Assert.DoesNotContain(madre, difusion.RecipientUserIds);
    }

    [Fact]
    public async Task ReportLocation_MarcaLaPeticionComoCumplida()
    {
        await using var h = ServiceHarness.Create();
        var (familia, madre, padre) = await EscenarioFamiliar(h);
        var peticion = await h.AddPendingRequestAsync(madre, padre, familia);

        var resultado = await h.Locations.ReportLocationAsync(padre, new ReportLocationDto(1m, 1m));

        h.Db.ChangeTracker.Clear();
        var actualizada = await h.Db.LocationRequests.SingleAsync(lr => lr.Id == peticion.Id);
        Assert.NotNull(actualizada.FulfilledAt);
        Assert.Equal(resultado.Id, actualizada.LocationId);
    }

    // -------------------------------------------------------------------------------
    // Consulta de ubicaciones e historial
    // -------------------------------------------------------------------------------

    [Fact]
    public async Task GetLatestLocation_UnExtranoNoPuedeConsultarla()
    {
        await using var h = ServiceHarness.Create();
        var (familia, _, padre) = await EscenarioFamiliar(h);
        await h.AddLocationAsync(padre, 1m, 1m, DateTime.UtcNow);
        var extrano = await h.AddUserAsync("0999000099");

        await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Locations.GetLatestLocationAsync(extrano.Id, familia, padre));
    }

    [Fact]
    public async Task GetLatestLocation_SinDatosDevuelveNull()
    {
        await using var h = ServiceHarness.Create();
        var (familia, madre, padre) = await EscenarioFamiliar(h);

        Assert.Null(await h.Locations.GetLatestLocationAsync(madre, familia, padre));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(999999)]
    public async Task GetLocationHistory_AcotaElLimiteEnLugarDeReventar(int limite)
    {
        // limit=-1 generaba un LIMIT negativo que PostgreSQL rechaza (500), y un limit
        // enorme volcaba el historial completo en una sola respuesta.
        await using var h = ServiceHarness.Create();
        var (familia, madre, padre) = await EscenarioFamiliar(h);
        for (var i = 1; i <= 3; i++)
        {
            await h.AddLocationAsync(padre, i, i, DateTime.UtcNow.AddMinutes(-i));
        }

        var historial = await h.Locations.GetLocationHistoryAsync(madre, familia, padre, limite);

        Assert.NotEmpty(historial);
        Assert.True(historial.Count <= 500);
    }

    [Fact]
    public async Task DeleteMyHistory_BorraSoloElHistorialPropio()
    {
        await using var h = ServiceHarness.Create();
        var (_, madre, padre) = await EscenarioFamiliar(h);
        await h.AddLocationAsync(padre, 1m, 1m, DateTime.UtcNow);
        await h.AddLocationAsync(padre, 2m, 2m, DateTime.UtcNow.AddMinutes(-5));
        await h.AddLocationAsync(madre, 3m, 3m, DateTime.UtcNow);

        var borradas = await h.Locations.DeleteMyHistoryAsync(padre);

        Assert.Equal(2, borradas);
        h.Db.ChangeTracker.Clear();
        Assert.Empty(await h.Db.Locations.Where(l => l.UserId == padre).ToListAsync());
        Assert.Single(await h.Db.Locations.Where(l => l.UserId == madre).ToListAsync());
    }

    // -------------------------------------------------------------------------------

    /// <summary>Familia con una madre y un padre. Devuelve (familiaId, madreId, padreId).</summary>
    private static async Task<(Guid familia, Guid madre, Guid padre)> EscenarioFamiliar(ServiceHarness h)
    {
        var familia = await h.AddFamilyAsync("Familia Castro");
        var madre = await h.AddUserAsync("0999000002", firstName: "Rosa");
        var padre = await h.AddUserAsync("0999000003", firstName: "Jorge");
        await h.AddMemberAsync(familia.Id, madre.Id, "admin");
        await h.AddMemberAsync(familia.Id, padre.Id);

        return (familia.Id, madre.Id, padre.Id);
    }
}
