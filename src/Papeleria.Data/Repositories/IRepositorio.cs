using System.Linq.Expressions;
using Papeleria.Domain.Common;

namespace Papeleria.Data.Repositories;

/// <summary>
/// Operaciones de acceso a datos comunes a cualquier entidad. Las consultas complejas
/// se construyen sobre <see cref="Consulta"/>, que devuelve un <see cref="IQueryable{T}"/>
/// para que el filtrado, el orden y la paginación se resuelvan en el motor de base de datos.
/// </summary>
public interface IRepositorio<T> where T : EntidadBase
{
    /// <summary>Consulta base. Sin rastreo por defecto: ideal para listados de solo lectura.</summary>
    IQueryable<T> Consulta(bool rastrear = false);

    Task<T?> ObtenerPorIdAsync(int id, CancellationToken ct = default);

    Task<T?> BuscarAsync(Expression<Func<T, bool>> predicado, CancellationToken ct = default);

    Task<List<T>> ListarAsync(Expression<Func<T, bool>>? predicado = null, CancellationToken ct = default);

    Task<bool> ExisteAsync(Expression<Func<T, bool>> predicado, CancellationToken ct = default);

    Task<int> ContarAsync(Expression<Func<T, bool>>? predicado = null, CancellationToken ct = default);

    Task AgregarAsync(T entidad, CancellationToken ct = default);

    Task AgregarRangoAsync(IEnumerable<T> entidades, CancellationToken ct = default);

    void Actualizar(T entidad);

    void Eliminar(T entidad);

    void EliminarRango(IEnumerable<T> entidades);
}
