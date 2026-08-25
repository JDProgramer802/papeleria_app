using Papeleria.Business.Dtos;

namespace Papeleria.Business.Services;

/// <summary>
/// Devoluciones parciales de una venta. Devolver dos cuadernos de una factura de
/// quince renglones no debería obligar a anularla entera y volver a facturar: eso
/// rompe el consecutivo y deja el histórico lleno de facturas fantasma.
/// </summary>
public interface IServicioDevoluciones
{
    /// <summary>
    /// Prepara la factura para devolver: cada renglón con lo vendido, lo ya devuelto
    /// antes y lo que todavía se puede devolver.
    /// </summary>
    Task<VentaDevolvibleDto> PrepararAsync(int ventaId, CancellationToken ct = default);

    /// <summary>
    /// Registra la devolución: repone las existencias de los productos, deja rastro en
    /// el kardex y saca del cajón el dinero reintegrado si la venta se cobró en efectivo.
    /// </summary>
    Task<DevolucionDto> RegistrarAsync(SolicitudDevolucion solicitud, CancellationToken ct = default);

    /// <summary>Devoluciones hechas sobre una factura.</summary>
    Task<List<DevolucionDto>> ListarPorVentaAsync(int ventaId, CancellationToken ct = default);

    /// <summary>Devoluciones de un periodo, para el reporte y el arqueo.</summary>
    Task<List<DevolucionDto>> ListarAsync(DateTime desde, DateTime hasta, CancellationToken ct = default);
}
