using Papeleria.Domain.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Domain.Entities;

/// <summary>
/// Artículo comercializado por la papelería. <see cref="StockActual"/> solo lo modifican
/// los servicios de negocio, y siempre acompañado de un movimiento de kardex.
/// </summary>
public class Producto : EntidadBase, IActivable
{
    /// <summary>Código interno único del artículo.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Código de barras EAN/UPC/Code128. Opcional pero único cuando existe.</summary>
    public string? CodigoBarras { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public int CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    public int? MarcaId { get; set; }
    public Marca? Marca { get; set; }

    public int UnidadMedidaId { get; set; }
    public UnidadMedida? UnidadMedida { get; set; }

    /// <summary>Costo unitario de la última compra (promedio ponderado al comprar).</summary>
    public decimal Costo { get; set; }

    public decimal PrecioVenta { get; set; }

    /// <summary>Porcentaje de IVA aplicado en la venta (0, 5 o 19 en Colombia).</summary>
    public decimal PorcentajeIva { get; set; }

    /// <summary>
    /// Distingue la mercancía de los servicios (fotocopias, impresiones, anillado).
    /// Un servicio se cobra igual pero no descuenta existencias ni toca el kardex.
    /// </summary>
    public TipoProducto Tipo { get; set; } = TipoProducto.Producto;

    /// <summary>
    /// Unidades de venta que trae la presentación con la que se compra. La caja de
    /// doce lápices se compra como una y se vende de a uno: aquí va el doce, para
    /// que al recibir la compra el inventario suba en unidades y el costo se reparta.
    /// En uno, se compra y se vende en la misma unidad.
    /// </summary>
    public decimal UnidadesPorPresentacion { get; set; } = 1;

    public decimal StockActual { get; set; }

    public decimal StockMinimo { get; set; }

    public decimal StockMaximo { get; set; }

    /// <summary>Ruta absoluta de la imagen dentro del almacén local de la aplicación.</summary>
    public string? ImagenPath { get; set; }

    public string? Ubicacion { get; set; }

    public string? Observaciones { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<VentaDetalle> VentaDetalles { get; set; } = new List<VentaDetalle>();

    public ICollection<CompraDetalle> CompraDetalles { get; set; } = new List<CompraDetalle>();

    public ICollection<MovimientoKardex> MovimientosKardex { get; set; } = new List<MovimientoKardex>();

    /// <summary>Utilidad unitaria en pesos sobre el costo actual.</summary>
    public decimal UtilidadUnitaria => PrecioVenta - Costo;

    /// <summary>Margen porcentual sobre el precio de venta.</summary>
    public decimal MargenPorcentaje => PrecioVenta <= 0 ? 0 : Math.Round((PrecioVenta - Costo) / PrecioVenta * 100m, 2);

    /// <summary>Los servicios no manejan existencias, así que nunca se agotan.</summary>
    public bool ControlaExistencias => Tipo == TipoProducto.Producto;

    public bool EstaAgotado => ControlaExistencias && StockActual <= 0;

    public bool EstaBajoMinimo => ControlaExistencias && StockActual > 0 && StockActual <= StockMinimo;

    /// <summary>Etiqueta de la presentación de compra: «Caja × 12».</summary>
    public string PresentacionTexto =>
        UnidadesPorPresentacion > 1 ? $"Presentación × {UnidadesPorPresentacion:N0}" : "Unidad";
}
