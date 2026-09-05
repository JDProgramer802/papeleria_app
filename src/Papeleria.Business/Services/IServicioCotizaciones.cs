using Papeleria.Business.Dtos;
using Papeleria.Domain.Common;

namespace Papeleria.Business.Services;

/// <summary>
/// Cotizaciones: el precio en firme que se le pasa a un cliente antes de hacer el
/// trabajo. No mueven existencias ni caja; solo al aceptarlas se convierten en venta.
/// </summary>
public interface IServicioCotizaciones
{
    Task<ResultadoPaginado<CotizacionResumenDto>> BuscarAsync(
        FiltroCotizaciones filtro, CancellationToken ct = default);

    Task<CotizacionDetalladaDto?> ObtenerDetalleAsync(int cotizacionId, CancellationToken ct = default);

    Task<CotizacionDetalladaDto> RegistrarAsync(
        SolicitudCotizacion solicitud, CancellationToken ct = default);

    /// <summary>
    /// El cliente aceptó: se factura con los precios que se le cotizaron, aunque hayan
    /// cambiado desde entonces. Recién aquí se descuenta el inventario y entra el dinero.
    /// </summary>
    Task<VentaDetalladaDto> ConvertirEnVentaAsync(
        int cotizacionId, SolicitudConversionCotizacion conversion, CancellationToken ct = default);

    /// <summary>El cliente no la tomó. Queda en el histórico, no se borra.</summary>
    Task RechazarAsync(int cotizacionId, CancellationToken ct = default);

    /// <summary>Genera el PDF de la cotización para imprimirla o enviarla.</summary>
    Task<string> GenerarDocumentoAsync(int cotizacionId, CancellationToken ct = default);
}
