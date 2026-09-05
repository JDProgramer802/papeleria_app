using Papeleria.Business.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Business.Dtos;

/// <summary>
/// Cálculo de una línea de documento. Se aplica primero el descuento y sobre la base
/// resultante se liquida el IVA, que es el orden exigido en la facturación colombiana.
/// </summary>
public abstract class LineaDocumentoBase
{
    public int ProductoId { get; set; }

    public decimal Cantidad { get; set; } = 1;

    public decimal PorcentajeDescuento { get; set; }

    public decimal PorcentajeIva { get; set; }

    /// <summary>Precio o costo unitario según el tipo de documento.</summary>
    public abstract decimal ValorUnitario { get; set; }

    /// <summary>Cantidad × valor unitario, antes de descuentos.</summary>
    public decimal Subtotal => Dinero.Redondear(Cantidad * ValorUnitario);

    public decimal ValorDescuento => Dinero.Porcentaje(Subtotal, PorcentajeDescuento);

    /// <summary>Base sobre la que se liquida el impuesto.</summary>
    public decimal BaseGravable => Dinero.Redondear(Subtotal - ValorDescuento);

    public decimal ValorIva => Dinero.Porcentaje(BaseGravable, PorcentajeIva);

    public decimal Total => Dinero.Redondear(BaseGravable + ValorIva);
}

/// <summary>Línea de una compra a proveedor.</summary>
public class LineaCompra : LineaDocumentoBase
{
    /// <summary>
    /// La cantidad viene en presentaciones de compra (cajas, paquetes) y no en
    /// unidades sueltas. El servicio la convierte usando el factor del producto,
    /// de modo que el inventario siempre queda en la unidad con la que se vende.
    /// </summary>
    public bool PorPresentacion { get; set; }

    private decimal _costoUnitario;

    public override decimal ValorUnitario
    {
        get => _costoUnitario;
        set => _costoUnitario = value;
    }

    public decimal CostoUnitario
    {
        get => _costoUnitario;
        set => _costoUnitario = value;
    }

    public string DescripcionProducto { get; set; } = string.Empty;
}

/// <summary>Línea del carrito del punto de venta.</summary>
public class LineaVenta : LineaDocumentoBase
{
    private decimal _precioUnitario;

    public override decimal ValorUnitario
    {
        get => _precioUnitario;
        set => _precioUnitario = value;
    }

    public decimal PrecioUnitario
    {
        get => _precioUnitario;
        set => _precioUnitario = value;
    }

    /// <summary>Costo del producto al momento de vender, para calcular la utilidad.</summary>
    public decimal CostoUnitario { get; set; }

    public string DescripcionProducto { get; set; } = string.Empty;
}

/// <summary>Datos con los que se registra una compra.</summary>
public class SolicitudCompra
{
    public int ProveedorId { get; set; }

    public string? NumeroFacturaProveedor { get; set; }

    public DateTime Fecha { get; set; } = DateTime.Now;

    public string? Observaciones { get; set; }

    public List<LineaCompra> Lineas { get; set; } = new();

    public decimal Subtotal => Dinero.Redondear(Lineas.Sum(l => l.Subtotal));

    public decimal TotalDescuento => Dinero.Redondear(Lineas.Sum(l => l.ValorDescuento));

    public decimal TotalIva => Dinero.Redondear(Lineas.Sum(l => l.ValorIva));

    public decimal Total => Dinero.Redondear(Subtotal - TotalDescuento + TotalIva);
}

/// <summary>Datos con los que se registra una venta desde el punto de venta.</summary>
public class SolicitudVenta
{
    public int ClienteId { get; set; }

    public MetodoPago MetodoPago { get; set; } = MetodoPago.Efectivo;

    /// <summary>Dinero entregado por el cliente; en pagos electrónicos coincide con el total.</summary>
    public decimal MontoRecibido { get; set; }

    /// <summary>Referencia del pago electrónico: aprobación, transferencia o Nequi.</summary>
    public string? ReferenciaPago { get; set; }

    public string? Observaciones { get; set; }

    public List<LineaVenta> Lineas { get; set; } = new();

    public decimal Subtotal => Dinero.Redondear(Lineas.Sum(l => l.Subtotal));

    public decimal TotalDescuento => Dinero.Redondear(Lineas.Sum(l => l.ValorDescuento));

    public decimal TotalIva => Dinero.Redondear(Lineas.Sum(l => l.ValorIva));

    public decimal Total => Dinero.Redondear(Subtotal - TotalDescuento + TotalIva);

    public decimal CostoTotal => Dinero.Redondear(Lineas.Sum(l => l.Cantidad * l.CostoUnitario));

    public decimal Cambio => Dinero.Redondear(Math.Max(MontoRecibido - Total, 0));

    public int CantidadArticulos => (int)Lineas.Sum(l => l.Cantidad);
}
