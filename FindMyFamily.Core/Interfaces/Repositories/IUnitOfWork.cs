namespace FindMyFamily.Core.Interfaces.Repositories;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta varias escrituras como una sola unidad atómica.
    /// Necesario para el borrado lógico de una cuenta, que toca tres tablas (users,
    /// family_members y devices) mediante operaciones que van directas a la base de datos:
    /// sin transacción, un fallo a mitad dejaría al usuario marcado como borrado pero
    /// todavía apareciendo como miembro de sus familias.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default);
}
