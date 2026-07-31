using Papeleria.Business.Dtos;

namespace Papeleria.Business.Services;

/// <summary>Tamaño del comprobante de venta.</summary>
public enum FormatoFactura
{
    /// <summary>Tirilla de 80 mm para impresora térmica de mostrador.</summary>
    Recibo80mm = 0,

    /// <summary>Hoja carta, para clientes que piden factura formal.</summary>
    Carta = 1
}

/// <summary>Datos que se imprimen en una etiqueta de producto.</summary>
public class EtiquetaProducto
{
    public required string Nombre { get; init; }

    public required string Codigo { get; init; }

    public string? CodigoBarras { get; init; }

    public decimal Precio { get; init; }

    public string? UnidadAbreviatura { get; init; }

    /// <summary>Cantidad de copias de esta etiqueta a imprimir.</summary>
    public int Copias { get; init; } = 1;
}

/// <summary>Generación de los documentos imprimibles del sistema en PDF.</summary>
public interface IServicioDocumentos
{
    Task<string> GenerarFacturaAsync(
        VentaDetalladaDto venta, FormatoFactura formato = FormatoFactura.Recibo80mm,
        string? rutaDestino = null, CancellationToken ct = default);

    Task<string> GenerarComprobanteCompraAsync(
        CompraDetalladaDto compra, string? rutaDestino = null, CancellationToken ct = default);

    /// <summary>Hoja de etiquetas con código de barras, tres por fila.</summary>
    Task<string> GenerarEtiquetasAsync(
        IEnumerable<EtiquetaProducto> etiquetas, string? rutaDestino = null, CancellationToken ct = default);

    Task<string> GenerarArqueoCajaAsync(
        CajaSesionDto sesion, ArqueoCajaDto arqueo, IReadOnlyList<MovimientoCajaDto> movimientos,
        string? rutaDestino = null, CancellationToken ct = default);
}
