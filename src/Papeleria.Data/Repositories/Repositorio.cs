using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Papeleria.Domain.Common;

namespace Papeleria.Data.Repositories;

/// <inheritdoc cref="IRepositorio{T}" />
public class Repositorio<T> : IRepositorio<T> where T : EntidadBase
{
    private readonly AppDbContext _contexto;
    private readonly DbSet<T> _conjunto;

    public Repositorio(AppDbContext contexto)
    {
        _contexto = contexto;
        _conjunto = contexto.Set<T>();
    }

    public IQueryable<T> Consulta(bool rastrear = false) =>
        rastrear ? _conjunto : _conjunto.AsNoTracking();

    public Task<T?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
        _conjunto.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<T?> BuscarAsync(Expression<Func<T, bool>> predicado, CancellationToken ct = default) =>
        _conjunto.FirstOrDefaultAsync(predicado, ct);

    public Task<List<T>> ListarAsync(Expression<Func<T, bool>>? predicado = null, CancellationToken ct = default)
    {
        var consulta = Consulta();
        if (predicado is not null)
        {
            consulta = consulta.Where(predicado);
        }

        return consulta.ToListAsync(ct);
    }

    public Task<bool> ExisteAsync(Expression<Func<T, bool>> predicado, CancellationToken ct = default) =>
        _conjunto.AsNoTracking().AnyAsync(predicado, ct);

    public Task<int> ContarAsync(Expression<Func<T, bool>>? predicado = null, CancellationToken ct = default) =>
        predicado is null
            ? _conjunto.AsNoTracking().CountAsync(ct)
            : _conjunto.AsNoTracking().CountAsync(predicado, ct);

    public async Task AgregarAsync(T entidad, CancellationToken ct = default) =>
        await _conjunto.AddAsync(entidad, ct).ConfigureAwait(false);

    public async Task AgregarRangoAsync(IEnumerable<T> entidades, CancellationToken ct = default) =>
        await _conjunto.AddRangeAsync(entidades, ct).ConfigureAwait(false);

    public void Actualizar(T entidad)
    {
        // Si la entidad ya está rastreada por el contexto no hace falta reasociarla.
        if (_contexto.Entry(entidad).State == EntityState.Detached)
        {
            _conjunto.Attach(entidad);
        }

        _contexto.Entry(entidad).State = EntityState.Modified;
    }

    public void Eliminar(T entidad) => _conjunto.Remove(entidad);

    public void EliminarRango(IEnumerable<T> entidades) => _conjunto.RemoveRange(entidades);
}
