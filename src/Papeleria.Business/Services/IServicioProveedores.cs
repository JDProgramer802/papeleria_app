using Papeleria.Business.Dtos;
using Papeleria.Domain.Common;
using Papeleria.Domain.Entities;

namespace Papeleria.Business.Services;

/// <summary>Administración de proveedores y consulta de su historial de compras.</summary>
public interface IServicioProveedores
{
    Task<ResultadoPaginado<Proveedor>> BuscarAsync(
        string? texto, bool soloActivos, int pagina, int tamanoPagina, CancellationToken ct = default);

    Task<List<Proveedor>> ListarActivosAsync(CancellationToken ct = default);

    Task<Proveedor?> ObtenerAsync(int id, CancellationToken ct = default);

    Task<Proveedor> CrearAsync(Proveedor proveedor, CancellationToken ct = default);

    Task ActualizarAsync(Proveedor proveedor, CancellationToken ct = default);

    Task EliminarAsync(int id, CancellationToken ct = default);

    Task CambiarEstadoAsync(int id, bool activo, CancellationToken ct = default);

    Task<List<CompraResumenDto>> ObtenerHistorialAsync(int proveedorId, int maximo = 200, CancellationToken ct = default);

    Task<ResumenTerceroDto> ObtenerResumenAsync(int proveedorId, CancellationToken ct = default);
}
