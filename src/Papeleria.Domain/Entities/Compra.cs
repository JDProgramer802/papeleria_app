using Papeleria.Domain.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Domain.Entities;

/// <summary>Documento de compra a un proveedor. Al registrarse incrementa existencias y kardex.</summary>
public class Compra : EntidadBase
{
    /// <summary>Consecutivo interno con prefijo, p. ej. «CMP-000123».</summary>
    public string Numero { get; set; } = string.Empty;

    /// <summary>Número de la factura física entregada por el proveedor.</summary>
    public string? NumeroFacturaProveedor { get; set; }

    public DateTime Fecha { get; set; } = DateTime.Now;

    public int ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public decimal Subtotal { get; set; }

    public decimal TotalDescuento { get; set; }

    public decimal TotalIva { get; set; }

    public decimal Total { get; set; }

    public EstadoCompra Estado { get; set; } = EstadoCompra.Registrada;

    public string? Observaciones { get; set; }

    public ICollection<CompraDetalle> Detalles { get; set; } = new List<CompraDetalle>();
}

/// <summary>Línea de una compra. Conserva el costo histórico aunque el producto cambie después.</summary>
public class CompraDetalle : EntidadBase
{
    public int CompraId { get; set; }
    public Compra? Compra { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    /// <summary>Descripción congelada al momento de la compra.</summary>
    public string DescripcionProducto { get; set; } = string.Empty;

    public decimal Cantidad { get; set; }

    public decimal CostoUnitario { get; set; }

    public decimal PorcentajeDescuento { get; set; }

    public decimal PorcentajeIva { get; set; }

    public decimal ValorDescuento { get; set; }

    public decimal ValorIva { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Total { get; set; }
}
