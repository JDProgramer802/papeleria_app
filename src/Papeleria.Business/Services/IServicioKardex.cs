using Papeleria.Business.Dtos;
using Papeleria.Data.Repositories;
using Papeleria.Domain.Common;
using Papeleria.Domain.Entities;
using Papeleria.Domain.Enums;

namespace Papeleria.Business.Services;

/// <summary>
/// Libro de existencias. Es el único punto autorizado para modificar
/// <see cref="Producto.StockActual"/>: cada cambio de inventario queda registrado.
/// </summary>
public interface IServicioKardex
{
    /// <summary>
    /// Aplica un movimiento al producto y deja el asiento correspondiente. Debe invocarse
    /// dentro de la transacción del documento que lo origina (compra, venta o ajuste).
    /// </summary>
    Task<MovimientoKardex> RegistrarAsync(
        IUnidadDeTrabajo unidad,
        Producto producto,
        TipoMovimientoKardex tipo,
        decimal cantidad,
        decimal costoUnitario,
        string motivo,
        string? documentoReferencia,
        int usuarioId,
        CancellationToken ct = default);

    Task<ResultadoPaginado<MovimientoKardexDto>> BuscarAsync(FiltroKardex filtro, CancellationToken ct = default);

    /// <summary>Movimientos más recientes, para el panel de actividad del dashboard.</summary>
    Task<List<MovimientoKardexDto>> ObtenerRecientesAsync(int cantidad = 10, CancellationToken ct = default);

    /// <summary>Kardex completo de un producto, ordenado cronológicamente.</summary>
    Task<List<MovimientoKardexDto>> ObtenerPorProductoAsync(
        int productoId, DateTime? desde = null, DateTime? hasta = null, CancellationToken ct = default);
}
