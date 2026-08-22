using Papeleria.Domain.Enums;

namespace Papeleria.Business.Dtos;

/// <summary>Fila del historial de compras.</summary>
public class CompraResumenDto
{
    public int Id { get; init; }

    public string Numero { get; init; } = string.Empty;

    public string? NumeroFacturaProveedor { get; init; }

    public DateTime Fecha { get; init; }

    public int ProveedorId { get; init; }

    public string ProveedorNombre { get; init; } = string.Empty;

    public string UsuarioNombre { get; init; } = string.Empty;

    public decimal Subtotal { get; init; }

    public decimal TotalDescuento { get; init; }

    public decimal TotalIva { get; init; }

    public decimal Total { get; init; }

    public int CantidadItems { get; init; }

    public EstadoCompra Estado { get; init; }

    public bool EstaAnulada => Estado == EstadoCompra.Anulada;

    public string EstadoTexto => Estado == EstadoCompra.Anulada ? "Anulada" : "Registrada";
}

/// <summary>Fila del historial de ventas.</summary>
public class VentaResumenDto
{
    public int Id { get; init; }

    public string NumeroFactura { get; init; } = string.Empty;

    public DateTime Fecha { get; init; }

    public int ClienteId { get; init; }

    public string ClienteNombre { get; init; } = string.Empty;

    public string UsuarioNombre { get; init; } = string.Empty;

    public decimal Subtotal { get; init; }

    public decimal TotalDescuento { get; init; }

    public decimal TotalIva { get; init; }

    public decimal Total { get; init; }

    public decimal CostoTotal { get; init; }

    public int CantidadItems { get; init; }

    public MetodoPago MetodoPago { get; init; }

    public EstadoVenta Estado { get; init; }

    public bool EstaAnulada => Estado == EstadoVenta.Anulada;

    public string EstadoTexto => Estado == EstadoVenta.Anulada ? "Anulada" : "Completada";

    public string MetodoPagoTexto => MetodoPago switch
    {
        MetodoPago.Efectivo => "Efectivo",
        MetodoPago.Tarjeta => "Tarjeta",
        MetodoPago.Transferencia => "Transferencia",
        MetodoPago.Credito => "Crédito",
        MetodoPago.Mixto => "Mixto",
        _ => MetodoPago.ToString()
    };

    /// <summary>Utilidad bruta de la factura, ya descontados los descuentos.</summary>
    public decimal Utilidad => Subtotal - TotalDescuento - CostoTotal;
}

/// <summary>Fila del ranking de productos más vendidos.</summary>
public class ProductoVendidoDto
{
    public int ProductoId { get; init; }

    public string Codigo { get; init; } = string.Empty;

    public string Nombre { get; init; } = string.Empty;

    public decimal CantidadVendida { get; init; }

    /// <summary>Importe facturado sin IVA y ya descontados los descuentos.</summary>
    public decimal MontoVendido { get; init; }

    public decimal Utilidad { get; init; }

    /// <summary>Participación sobre el total facturado del periodo, en porcentaje.</summary>
    public decimal Participacion { get; set; }
}

/// <summary>Cifras acumuladas de un tercero, mostradas en su ficha.</summary>
public class ResumenTerceroDto
{
    public int CantidadDocumentos { get; init; }

    public decimal MontoTotal { get; init; }

    public DateTime? UltimaFecha { get; init; }

    public decimal PromedioDocumento =>
        CantidadDocumentos == 0 ? 0 : Math.Round(MontoTotal / CantidadDocumentos, 2);
}

/// <summary>
/// Cifras acumuladas de un conjunto de ventas. Se calcula sobre todo el rango
/// filtrado, no sobre la página que se está mostrando.
/// </summary>
public class ResumenVentasDto
{
    public int CantidadFacturas { get; init; }

    public decimal TotalFacturado { get; init; }

    /// <summary>Utilidad bruta: base gravable menos el costo de la mercancía vendida.</summary>
    public decimal TotalUtilidad { get; init; }

    public int CantidadAnuladas { get; init; }

    public decimal TotalAnulado { get; init; }

    /// <summary>Número de líneas facturadas, no de unidades.</summary>
    public int LineasFacturadas { get; init; }

    public decimal TicketPromedio =>
        CantidadFacturas == 0 ? 0 : Math.Round(TotalFacturado / CantidadFacturas, 2);

    public decimal MargenPorcentaje =>
        TotalFacturado == 0 ? 0 : Math.Round(TotalUtilidad / TotalFacturado * 100m, 1);

    public bool HayAnuladas => CantidadAnuladas > 0;
}
