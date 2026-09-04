using Papeleria.Business.Dtos;
using Papeleria.Domain.Common;
using Papeleria.Domain.Entities;

namespace Papeleria.Business.Services;

/// <summary>Administración del maestro de productos.</summary>
public interface IServicioProductos
{
    /// <summary>Listado paginado con filtros; el orden y la paginación se resuelven en SQL.</summary>
    Task<ResultadoPaginado<ProductoListadoDto>> BuscarAsync(FiltroProductos filtro, CancellationToken ct = default);

    Task<Producto?> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>Búsqueda rápida para el punto de venta (código, código de barras o nombre).</summary>
    Task<List<ProductoPosDto>> BuscarParaVentaAsync(string? texto, int maximo = 40, CancellationToken ct = default);

    /// <summary>
    /// La misma búsqueda, pero sin servicios: a un proveedor se le compra mercancía, y
    /// una fotocopia no se recibe en una factura de compra.
    /// </summary>
    Task<List<ProductoPosDto>> BuscarParaCompraAsync(string? texto, int maximo = 40, CancellationToken ct = default);

    /// <summary>Coincidencia exacta por código de barras o código interno, para el lector.</summary>
    Task<ProductoPosDto?> BuscarPorCodigoExactoAsync(string codigo, CancellationToken ct = default);

    Task<Producto> CrearAsync(Producto producto, CancellationToken ct = default);

    Task ActualizarAsync(Producto producto, CancellationToken ct = default);

    Task EliminarAsync(int id, CancellationToken ct = default);

    Task CambiarEstadoAsync(int id, bool activo, CancellationToken ct = default);

    /// <summary>Crea una copia del producto con código nuevo y existencias en cero.</summary>
    Task<Producto> DuplicarAsync(int id, CancellationToken ct = default);

    /// <summary>Sugiere el siguiente código interno disponible (PRD-0001, PRD-0002…).</summary>
    Task<string> SugerirCodigoAsync(CancellationToken ct = default);

    /// <summary>Genera un código de barras EAN-13 válido y libre para el producto.</summary>
    Task<string> GenerarCodigoBarrasAsync(CancellationToken ct = default);

    Task<List<ProductoListadoDto>> ListarBajoMinimoAsync(int maximo = 100, CancellationToken ct = default);

    Task<List<ProductoListadoDto>> ListarAgotadosAsync(int maximo = 100, CancellationToken ct = default);
}
