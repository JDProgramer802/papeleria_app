using Papeleria.Domain.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Domain.Entities;

/// <summary>Factura de venta generada en el punto de venta.</summary>
public class Venta : EntidadBase
{
    /// <summary>Consecutivo con prefijo configurable, p. ej. «FV-000045».</summary>
    public string NumeroFactura { get; set; } = string.Empty;

    public DateTime Fecha { get; set; } = DateTime.Now;

    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>Sesión de caja en la que se registró la venta.</summary>
    public int? CajaSesionId { get; set; }
    public CajaSesion? CajaSesion { get; set; }

    public decimal Subtotal { get; set; }

    public decimal TotalDescuento { get; set; }

    public decimal TotalIva { get; set; }

    public decimal Total { get; set; }

    /// <summary>Costo total de la mercancía vendida, congelado para el cálculo de utilidad.</summary>
    public decimal CostoTotal { get; set; }

    public MetodoPago MetodoPago { get; set; } = MetodoPago.Efectivo;

    public decimal MontoRecibido { get; set; }

    public decimal Cambio { get; set; }

    /// <summary>
    /// Número de aprobación, referencia de la transferencia o los últimos dígitos del
    /// teléfono de Nequi. Sin esto, cuadrar el día contra el extracto es adivinar.
    /// </summary>
    public string? ReferenciaPago { get; set; }

    public EstadoVenta Estado { get; set; } = EstadoVenta.Completada;

    public DateTime? FechaAnulacion { get; set; }

    public string? MotivoAnulacion { get; set; }

    public int? UsuarioAnulacionId { get; set; }

    public string? Observaciones { get; set; }

    public ICollection<VentaDetalle> Detalles { get; set; } = new List<VentaDetalle>();

    /// <summary>Utilidad bruta de la factura (total sin IVA menos costo).</summary>
    public decimal Utilidad => Subtotal - TotalDescuento - CostoTotal;
}

/// <summary>Línea de una factura de venta con precios y costos congelados.</summary>
public class VentaDetalle : EntidadBase
{
    public int VentaId { get; set; }
    public Venta? Venta { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public string DescripcionProducto { get; set; } = string.Empty;

    public decimal Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }

    /// <summary>Costo unitario al momento de la venta, para el reporte de ganancias.</summary>
    public decimal CostoUnitario { get; set; }

    public decimal PorcentajeDescuento { get; set; }

    public decimal PorcentajeIva { get; set; }

    public decimal ValorDescuento { get; set; }

    public decimal ValorIva { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Total { get; set; }
}
