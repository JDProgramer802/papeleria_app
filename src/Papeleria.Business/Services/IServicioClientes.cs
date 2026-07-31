using Papeleria.Business.Dtos;
using Papeleria.Domain.Common;
using Papeleria.Domain.Entities;

namespace Papeleria.Business.Services;

/// <summary>Administración de clientes y consulta de su historial de compras.</summary>
public interface IServicioClientes
{
    Task<ResultadoPaginado<Cliente>> BuscarAsync(
        string? texto, bool soloActivos, int pagina, int tamanoPagina, CancellationToken ct = default);

    Task<List<Cliente>> ListarActivosAsync(CancellationToken ct = default);

    /// <summary>Devuelve el cliente «Consumidor final», que el POS usa por defecto.</summary>
    Task<Cliente> ObtenerConsumidorFinalAsync(CancellationToken ct = default);

    Task<Cliente?> ObtenerAsync(int id, CancellationToken ct = default);

    Task<Cliente> CrearAsync(Cliente cliente, CancellationToken ct = default);

    Task ActualizarAsync(Cliente cliente, CancellationToken ct = default);

    Task EliminarAsync(int id, CancellationToken ct = default);

    Task CambiarEstadoAsync(int id, bool activo, CancellationToken ct = default);

    Task<List<VentaResumenDto>> ObtenerHistorialAsync(int clienteId, int maximo = 200, CancellationToken ct = default);

    Task<ResumenTerceroDto> ObtenerResumenAsync(int clienteId, CancellationToken ct = default);
}
