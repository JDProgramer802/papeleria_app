using Microsoft.EntityFrameworkCore.Storage;
using Papeleria.Domain.Common;

namespace Papeleria.Data.Repositories;

/// <summary>
/// Unidad de trabajo sobre un único <see cref="AppDbContext"/>. Agrupa los repositorios
/// y permite confirmar varios cambios en una sola transacción (por ejemplo, una venta que
/// toca productos, kardex, caja y factura).
/// </summary>
public interface IUnidadDeTrabajo : IDisposable, IAsyncDisposable
{
    IRepositorio<T> Repositorio<T>() where T : EntidadBase;

    /// <summary>Acceso directo al contexto para consultas que cruzan varias entidades.</summary>
    AppDbContext Contexto { get; }

    Task<int> GuardarCambiosAsync(CancellationToken ct = default);

    Task<IDbContextTransaction> IniciarTransaccionAsync(CancellationToken ct = default);

    /// <summary>Ejecuta una operación dentro de una transacción y la revierte ante cualquier error.</summary>
    Task<TResultado> EjecutarEnTransaccionAsync<TResultado>(
        Func<CancellationToken, Task<TResultado>> operacion, CancellationToken ct = default);
}

/// <summary>Crea unidades de trabajo de vida corta, una por operación.</summary>
public interface IUnidadDeTrabajoFactory
{
    IUnidadDeTrabajo Crear();
}
