using Papeleria.Business.Common;

namespace Papeleria.Business.Dtos;

/// <summary>
/// Renglón de una factura visto desde la devolución: lo que se vendió, lo que ya
/// se devolvió antes y, por tanto, lo que todavía puede devolverse.
/// </summary>
public class LineaDevolvibleDto
{
    public int ProductoId { get; init; }

    public string Codigo { get; init; } = string.Empty;

    public string Descripcion { get; init; } = string.Empty;

    public string UnidadAbreviatura { get; init; } = string.Empty;

    public decimal CantidadVendida { get; init; }

    public decimal CantidadDevuelta { get; init; }

    /// <summary>Precio neto por unidad, con el descuento de la línea ya aplicado.</summary>
    public decimal ValorUnitario { get; init; }

    public decimal CostoUnitario { get; init; }

    /// <summary>Si el producto vuelve al inventario; los servicios no.</summary>
    public bool ReponeInventario { get; init; }

    public decimal Disponible => Math.Max(CantidadVendida - CantidadDevuelta, 0);

    public bool SePuedeDevolver => Disponible > 0;
}

/// <summary>Factura preparada para devolver, con sus renglones y lo ya devuelto.</summary>
public class VentaDevolvibleDto
{
    public int VentaId { get; init; }

    public string NumeroFactura { get; init; } = string.Empty;

    public DateTime Fecha { get; init; }

    public string ClienteNombre { get; init; } = string.Empty;

    public decimal Total { get; init; }

    /// <summary>Importe ya reintegrado en devoluciones anteriores.</summary>
    public decimal TotalDevuelto { get; init; }

    public IReadOnlyList<LineaDevolvibleDto> Lineas { get; init; } = Array.Empty<LineaDevolvibleDto>();

    public bool HayAlgoQueDevolver => Lineas.Any(l => l.SePuedeDevolver);

    public bool TieneDevolucionesPrevias => TotalDevuelto > 0;
}

/// <summary>Cantidad concreta que se devuelve de un renglón.</summary>
public class LineaDevolucion
{
    public int ProductoId { get; set; }

    public decimal Cantidad { get; set; }
}

/// <summary>Datos con los que se registra la devolución.</summary>
public class SolicitudDevolucion
{
    public int VentaId { get; set; }

    public string Motivo { get; set; } = string.Empty;

    public List<LineaDevolucion> Lineas { get; set; } = new();
}

/// <summary>Devolución ya registrada.</summary>
public class DevolucionDto
{
    public int Id { get; init; }

    public string Numero { get; init; } = string.Empty;

    public DateTime Fecha { get; init; }

    public string NumeroFactura { get; init; } = string.Empty;

    public string ClienteNombre { get; init; } = string.Empty;

    public string UsuarioNombre { get; init; } = string.Empty;

    public string Motivo { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public int CantidadLineas { get; init; }
}
