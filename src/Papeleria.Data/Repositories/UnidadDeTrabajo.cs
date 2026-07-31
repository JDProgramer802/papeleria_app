using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Papeleria.Domain.Common;

namespace Papeleria.Data.Repositories;

/// <inheritdoc cref="IUnidadDeTrabajo" />
public sealed class UnidadDeTrabajo : IUnidadDeTrabajo
{
    private readonly ConcurrentDictionary<Type, object> _repositorios = new();
    private bool _liberado;

    public UnidadDeTrabajo(AppDbContext contexto) => Contexto = contexto;

    public AppDbContext Contexto { get; }

    public IRepositorio<T> Repositorio<T>() where T : EntidadBase =>
        (IRepositorio<T>)_repositorios.GetOrAdd(typeof(T), _ => new Repositorio<T>(Contexto));

    public Task<int> GuardarCambiosAsync(CancellationToken ct = default) =>
        Contexto.SaveChangesAsync(ct);

    public Task<IDbContextTransaction> IniciarTransaccionAsync(CancellationToken ct = default) =>
        Contexto.Database.BeginTransactionAsync(ct);

    public async Task<TResultado> EjecutarEnTransaccionAsync<TResultado>(
        Func<CancellationToken, Task<TResultado>> operacion, CancellationToken ct = default)
    {
        // Si ya existe una transacción activa se reutiliza para no anidar.
        if (Contexto.Database.CurrentTransaction is not null)
        {
            return await operacion(ct).ConfigureAwait(false);
        }

        await using var transaccion = await IniciarTransaccionAsync(ct).ConfigureAwait(false);

        try
        {
            var resultado = await operacion(ct).ConfigureAwait(false);
            await transaccion.CommitAsync(ct).ConfigureAwait(false);
            return resultado;
        }
        catch
        {
            await transaccion.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    public void Dispose()
    {
        if (_liberado) return;
        _liberado = true;
        Contexto.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_liberado) return;
        _liberado = true;
        await Contexto.DisposeAsync().ConfigureAwait(false);
    }
}

/// <inheritdoc cref="IUnidadDeTrabajoFactory" />
public sealed class UnidadDeTrabajoFactory : IUnidadDeTrabajoFactory
{
    private readonly IDbContextFactory<AppDbContext> _fabricaContexto;

    public UnidadDeTrabajoFactory(IDbContextFactory<AppDbContext> fabricaContexto) =>
        _fabricaContexto = fabricaContexto;

    public IUnidadDeTrabajo Crear() => new UnidadDeTrabajo(_fabricaContexto.CreateDbContext());
}
