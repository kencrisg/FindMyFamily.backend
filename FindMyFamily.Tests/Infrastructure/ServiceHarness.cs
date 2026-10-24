using FindMyFamily.Core.Entities;
using FindMyFamily.Core.Interfaces.Services;
using FindMyFamily.Core.Services;
using FindMyFamily.Infrastructure.Persistence;
using FindMyFamily.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FindMyFamily.Tests.Infrastructure;

/// <summary>
/// Monta los servicios de dominio sobre una base de datos SQLite en memoria con los
/// repositorios y el DbContext reales.
///
/// Se usa SQLite y no el proveedor InMemory porque el código depende de cosas que InMemory
/// no sabe ejecutar: `ExecuteUpdateAsync`/`ExecuteDeleteAsync` (revocación de tokens,
/// borrado de pertenencias) y transacciones reales. Probar con repositorios falsos dejaría
/// sin cubrir justo lo que más se rompió: el filtro global de borrado lógico, las
/// relaciones en cascada y la atomicidad del borrado de una cuenta.
/// </summary>
public sealed class ServiceHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public ApplicationDbContext Db { get; }
    public FakePasswordHasher PasswordHasher { get; } = new();
    public FakeJwtTokenGenerator JwtTokenGenerator { get; } = new();
    public FakeRefreshTokenHasher RefreshTokenHasher { get; } = new();
    public FakePushNotificationService Push { get; } = new();
    public FakeLocationRealtimeNotifier Notifier { get; } = new();

    public IAuthService Auth { get; }
    public IUserService Users { get; }
    public IFamilyService Families { get; }
    public ILocationService Locations { get; }
    public IDeviceService Devices { get; }

    private ServiceHarness()
    {
        // ":memory:" vive mientras la conexión esté abierta: se mantiene en este campo.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new ApplicationDbContext(options);
        Db.Database.EnsureCreated();

        var unitOfWork = new UnitOfWork(Db);
        var userRepository = new UserRepository(Db);
        var deviceRepository = new DeviceRepository(Db);
        var familyRepository = new FamilyRepository(Db);
        var locationRepository = new LocationRepository(Db);
        var locationRequestRepository = new LocationRequestRepository(Db);

        Auth = new AuthService(userRepository, deviceRepository, unitOfWork,
            PasswordHasher, JwtTokenGenerator, RefreshTokenHasher);

        Users = new UserService(userRepository, familyRepository, deviceRepository,
            unitOfWork, PasswordHasher);

        Families = new FamilyService(familyRepository, userRepository, deviceRepository,
            unitOfWork, PasswordHasher);

        Locations = new LocationService(locationRepository, locationRequestRepository,
            familyRepository, deviceRepository, unitOfWork, Push, Notifier);

        Devices = new DeviceService(deviceRepository, userRepository, unitOfWork);
    }

    public static ServiceHarness Create() => new();

    // ---------------------------------------------------------------------------------
    // Ayudantes de datos. Escriben directo en el DbContext para que el escenario de
    // partida de cada test sea evidente y no dependa de los propios endpoints.
    // ---------------------------------------------------------------------------------

    public async Task<User> AddUserAsync(
        string phone,
        string role = "user",
        string password = "Valida2026",
        string firstName = "Nombre",
        string lastName = "Apellido",
        DateTime? deletedAt = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phone,
            PasswordHash = PasswordHasher.HashPassword(password),
            Role = role,
            CreatedAt = DateTime.UtcNow,
            DeletedAt = deletedAt
        };

        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return user;
    }

    public async Task<Family> AddFamilyAsync(string name = "Familia Test")
    {
        var family = new Family
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTime.UtcNow
        };

        Db.Families.Add(family);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return family;
    }

    public async Task AddMemberAsync(Guid familyId, Guid userId, string role = "member")
    {
        Db.FamilyMembers.Add(new FamilyMember
        {
            FamilyId = familyId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        });

        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    public async Task<Device> AddDeviceAsync(
        Guid userId,
        string? fcmToken = "fcm-token-de-prueba",
        bool isActive = true,
        string? installationId = null,
        string? refreshToken = null,
        DateTime? refreshTokenExpiry = null)
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            InstallationId = installationId,
            FcmToken = fcmToken,
            IsActive = isActive,
            LastActiveAt = DateTime.UtcNow,
            RefreshTokenHash = refreshToken == null ? null : RefreshTokenHasher.Hash(refreshToken),
            RefreshTokenExpiry = refreshTokenExpiry
        };

        Db.Devices.Add(device);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return device;
    }

    public async Task<Location> AddLocationAsync(Guid userId, decimal lat, decimal lon, DateTime capturedAt)
    {
        var location = new Location
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Latitude = lat,
            Longitude = lon,
            CapturedAt = capturedAt
        };

        Db.Locations.Add(location);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return location;
    }

    public async Task<LocationRequest> AddPendingRequestAsync(
        Guid requesterId, Guid targetUserId, Guid familyId, DateTime? expiresAt = null)
    {
        var request = new LocationRequest
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            TargetUserId = targetUserId,
            FamilyId = familyId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMinutes(10)
        };

        Db.LocationRequests.Add(request);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return request;
    }

    /// <summary>
    /// Relee un dispositivo desde la base de datos. Imprescindible al comprobar efectos de
    /// `ExecuteUpdateAsync`: esa operación va directa al motor y no actualiza las entidades
    /// ya rastreadas, así que una consulta normal devolvería el valor viejo en memoria.
    /// </summary>
    public async Task<Device?> ReloadDeviceAsync(Guid deviceId)
    {
        Db.ChangeTracker.Clear();
        return await Db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deviceId);
    }

    public async Task<List<Device>> ReloadDevicesOfUserAsync(Guid userId)
    {
        Db.ChangeTracker.Clear();
        return await Db.Devices.AsNoTracking().Where(d => d.UserId == userId).ToListAsync();
    }

    /// <summary>Relee un usuario saltándose el filtro de borrado lógico.</summary>
    public async Task<User?> ReloadUserIncludingDeletedAsync(Guid id)
    {
        Db.ChangeTracker.Clear();
        return await Db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
