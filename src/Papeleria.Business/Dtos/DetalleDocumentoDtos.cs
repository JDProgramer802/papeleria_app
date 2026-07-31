using Papeleria.Domain.Enums;

namespace Papeleria.Business.Dtos;

/// <summary>Documento de compra completo, con sus líneas, para consulta e impresión.</summary>
public class CompraDetalladaDto
{
    public int Id { get; init; }

    public string Numero { get; init; } = string.Empty;

    public string? NumeroFacturaProveedor { get; init; }

    public DateTime Fecha { get; init; }

    public string ProveedorNombre { get; init; } = string.Empty;

    public string? ProveedorNit { get; init; }

    public string? ProveedorTelefono { get; init; }

    public string UsuarioNombre { get; init; } = string.Empty;

    public decimal Subtotal { get; init; }

    public decimal TotalDescuento { get; init; }

    public decimal TotalIva { get; init; }

    public decimal Total { get; init; }

    public EstadoCompra Estado { get; init; }

    public string? Observaciones { get; init; }

    public List<LineaDocumentoDto> Lineas { get; init; } = new();
}

/// <summary>Factura de venta completa, con sus líneas, para reimpresión y consulta.</summary>
public class VentaDetalladaDto
{
    public int Id { get; init; }

    public string NumeroFactura { get; init; } = string.Empty;

    public DateTime Fecha { get; init; }

    public string ClienteNombre { get; init; } = string.Empty;

    public string? ClienteDocumento { get; init; }

    public string? ClienteTelefono { get; init; }

    public string? ClienteDireccion { get; init; }

    public string UsuarioNombre { get; init; } = string.Empty;

    public decimal Subtotal { get; init; }

    public decimal TotalDescuento { get; init; }

    public decimal TotalIva { get; init; }

    public decimal Total { get; init; }

    public decimal CostoTotal { get; init; }

    public MetodoPago MetodoPago { get; init; }

    public decimal MontoRecibido { get; init; }

    public decimal Cambio { get; init; }

    public EstadoVenta Estado { get; init; }

    public DateTime? FechaAnulacion { get; init; }

    public string? MotivoAnulacion { get; init; }

    public string? Observaciones { get; init; }

    public List<LineaDocumentoDto> Lineas { get; init; } = new();

    public decimal Utilidad => Subtotal - TotalDescuento - CostoTotal;

    public int CantidadArticulos => (int)Lineas.Sum(l => l.Cantidad);
}

/// <summary>Línea de un documento impreso o consultado.</summary>
public class LineaDocumentoDto
{
    public int ProductoId { get; init; }

    public string Codigo { get; init; } = string.Empty;

    public string Descripcion { get; init; } = string.Empty;

    public string UnidadAbreviatura { get; init; } = string.Empty;

    public decimal Cantidad { get; init; }

    public decimal ValorUnitario { get; init; }

    public decimal PorcentajeDescuento { get; init; }

    public decimal PorcentajeIva { get; init; }

    public decimal ValorDescuento { get; init; }

    public decimal ValorIva { get; init; }

    public decimal Subtotal { get; init; }

    public decimal Total { get; init; }
}
