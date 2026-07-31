using Papeleria.Business.Dtos;
using Papeleria.Domain.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Business.Services;

/// <summary>Criterios de consulta del historial de ventas.</summary>
public class FiltroVentas
{
    public string? Texto { get; set; }

    public int? ClienteId { get; set; }

    public int? UsuarioId { get; set; }

    public DateTime? Desde { get; set; }

    public DateTime? Hasta { get; set; }

    public MetodoPago? MetodoPago { get; set; }

    public bool IncluirAnuladas { get; set; } = true;

    public int Pagina { get; set; } = 1;

    public int TamanoPagina { get; set; } = 25;
}

/// <summary>
/// Punto de venta. Facturar descuenta existencias, escribe en el kardex y mueve la caja
/// dentro de una única transacción; si algo falla, no queda nada a medias.
/// </summary>
public interface IServicioVentas
{
    Task<ResultadoPaginado<VentaResumenDto>> BuscarAsync(FiltroVentas filtro, CancellationToken ct = default);

    Task<VentaDetalladaDto?> ObtenerDetalleAsync(int ventaId, CancellationToken ct = default);

    /// <summary>Registra la factura. Exige caja abierta y existencias suficientes.</summary>
    Task<VentaDetalladaDto> RegistrarAsync(SolicitudVenta solicitud, CancellationToken ct = default);

    /// <summary>Anula la factura devolviendo la mercancía al inventario. Reservado al administrador.</summary>
    Task AnularAsync(int ventaId, string motivo, CancellationToken ct = default);

    /// <summary>Productos más vendidos en un periodo, para el dashboard y los reportes.</summary>
    Task<List<ProductoVendidoDto>> ObtenerMasVendidosAsync(
        DateTime desde, DateTime hasta, int cantidad = 10, CancellationToken ct = default);
}
