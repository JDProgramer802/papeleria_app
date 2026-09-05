using Papeleria.Domain.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Domain.Entities;

/// <summary>
/// Precio en firme que se le pasa a un cliente antes de hacer el trabajo.
///
/// En una papelería que además imprime y diseña, casi nada grande se vende sobre la
/// marcha: el colegio pide precio por doscientos cuadernos marcados, la oficina por
/// mil tarjetas. Esa cuenta se hacía en papel o en el celular, y cuando el cliente
/// volvía a los ocho días nadie se acordaba de qué se le había dicho.
///
/// Una cotización no toca existencias ni caja: es un documento. Solo cuando el
/// cliente acepta se convierte en venta, y ahí sí se descuenta y se cobra.
/// </summary>
public class Cotizacion : EntidadBase
{
    public string Numero { get; set; } = string.Empty;

    public DateTime Fecha { get; set; } = DateTime.Now;

    /// <summary>Hasta cuándo se respetan estos precios.</summary>
    public DateTime FechaVence { get; set; } = DateTime.Today.AddDays(15);

    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public EstadoCotizacion Estado { get; set; } = EstadoCotizacion.Vigente;

    public decimal Subtotal { get; set; }

    public decimal TotalDescuento { get; set; }

    public decimal TotalIva { get; set; }

    public decimal Total { get; set; }

    /// <summary>Condiciones, tiempos de entrega, anticipo… lo que se pactó de palabra.</summary>
    public string? Observaciones { get; set; }

    /// <summary>Factura que salió de esta cotización, cuando el cliente aceptó.</summary>
    public int? VentaId { get; set; }
    public Venta? Venta { get; set; }

    public ICollection<CotizacionDetalle> Detalles { get; set; } = new List<CotizacionDetalle>();
}

/// <summary>
/// Renglón cotizado. Guarda el precio del día en que se cotizó: si mañana sube el
/// costo, el cliente que trae la cotización en la mano paga lo que se le dijo.
/// </summary>
public class CotizacionDetalle : EntidadBase
{
    public int CotizacionId { get; set; }
    public Cotizacion? Cotizacion { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    /// <summary>Nombre con el que se cotizó, por si el producto se renombra después.</summary>
    public string DescripcionProducto { get; set; } = string.Empty;

    public decimal Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }

    public decimal CostoUnitario { get; set; }

    public decimal PorcentajeDescuento { get; set; }

    public decimal PorcentajeIva { get; set; }

    public decimal ValorDescuento { get; set; }

    public decimal ValorIva { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Total { get; set; }
}
