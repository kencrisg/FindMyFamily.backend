using FindMyFamily.Core.Entities;

namespace FindMyFamily.Core.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetAllUsersAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    /// <summary>¿Existe ya algún Administrador de Sistema? Usado por el bootstrap inicial.</summary>
    Task<bool> AnySystemAdminAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Comprueba si el usuario es Administrador de Sistema sin cargar la entidad. Las
    /// comprobaciones de permisos solo necesitan el rol: cargar el usuario con sus
    /// dispositivos y familias en cada petición era trabajo tirado.
    /// </summary>
    Task<bool> IsSystemAdminAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Si el usuario comparte su ubicación, sin cargar la entidad.</summary>
    Task<bool> IsLocationSharingEnabledAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Comprueba el teléfono incluyendo las cuentas borradas lógicamente. El número es la
    /// identidad de login y su índice único es global, así que una cuenta borrada sigue
    /// ocupando su teléfono: hay que detectarlo para devolver un conflicto claro en lugar
    /// de dejar que falle la restricción en base de datos.
    /// </summary>
    Task<User?> GetByPhoneNumberIncludingDeletedAsync(string phoneNumber, CancellationToken cancellationToken = default);

    /// <summary>Recupera un usuario aunque esté borrado lógicamente (para restaurarlo).</summary>
    Task<User?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetDeletedUsersAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    void Update(User user);

    /// <summary>Borrado físico. Reservado a casos excepcionales; el flujo normal es el lógico.</summary>
    void Delete(User user);
}
