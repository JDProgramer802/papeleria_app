using Papeleria.Business.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Business.Dtos;

/// <summary>Cotización tal como se lista en la grilla.</summary>
public class CotizacionResumenDto
{
    public int Id { get; init; }

    public string Numero { get; init; } = string.Empty;

    public DateTime Fecha { get; init; }

    public DateTime FechaVence { get; init; }

    public string ClienteNombre { get; init; } = string.Empty;

    public string UsuarioNombre { get; init; } = string.Empty;

    public EstadoCotizacion Estado { get; init; }

    public decimal Total { get; init; }

    public int CantidadItems { get; init; }

    public int? VentaId { get; init; }

    public string? NumeroFactura { get; init; }

    /// <summary>
    /// Vencida no se guarda en la base: se deduce de la fecha. Así ninguna cotización
    /// aparece como vigente cuando ya se le pasó el plazo, sin tener que repasarlas.
    /// </summary>
    public bool EstaVencida =>
        Estado == EstadoCotizacion.Vigente && FechaVence.Date < DateTime.Today;

    public bool SePuedeConvertir => Estado == EstadoCotizacion.Vigente;

    public string EstadoTexto => Estado switch
    {
        EstadoCotizacion.Aceptada => "Aceptada",
        EstadoCotizacion.Rechazada => "Rechazada",
        _ => EstaVencida ? "Vencida" : "Vigente"
    };

    public string VigenciaTexto
    {
        get
        {
            if (Estado != EstadoCotizacion.Vigente)
            {
                return Formatos.Fecha(FechaVence);
            }

            var dias = (FechaVence.Date - DateTime.Today).Days;

            return dias switch
            {
                < 0 => $"Venció hace {-dias} día(s)",
                0 => "Vence hoy",
                1 => "Vence mañana",
                _ => $"Vence en {dias} días"
            };
        }
    }
}

/// <summary>Cotización completa, con sus renglones.</summary>
public class CotizacionDetalladaDto : CotizacionResumenDto
{
    public int ClienteId { get; init; }

    public string? ClienteDocumento { get; init; }

    public string? ClienteTelefono { get; init; }

    public decimal Subtotal { get; init; }

    public decimal TotalDescuento { get; init; }

    public decimal TotalIva { get; init; }

    public string? Observaciones { get; init; }

    public IReadOnlyList<CotizacionLineaDto> Lineas { get; init; } = Array.Empty<CotizacionLineaDto>();
}

/// <summary>Renglón de la cotización.</summary>
public class CotizacionLineaDto
{
    public int ProductoId { get; init; }

    public string Codigo { get; init; } = string.Empty;

    public string Descripcion { get; init; } = string.Empty;

    public decimal Cantidad { get; init; }

    public decimal PrecioUnitario { get; init; }

    public decimal PorcentajeDescuento { get; init; }

    public decimal PorcentajeIva { get; init; }

    public decimal Total { get; init; }
}

/// <summary>Criterios de búsqueda del listado de cotizaciones.</summary>
public class FiltroCotizaciones
{
    public string? Texto { get; set; }

    public DateTime Desde { get; set; } = DateTime.Today.AddMonths(-1);

    public DateTime Hasta { get; set; } = DateTime.Today;

    public int? ClienteId { get; set; }

    public EstadoCotizacion? Estado { get; set; }

    /// <summary>Deja fuera las que ya se pasaron de fecha.</summary>
    public bool SoloVigentes { get; set; }

    public int Pagina { get; set; } = 1;

    public int TamanoPagina { get; set; } = 25;
}

/// <summary>Renglón que se quiere cotizar.</summary>
public class LineaCotizacion
{
    public int ProductoId { get; set; }

    public decimal Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }

    public decimal CostoUnitario { get; set; }

    public decimal PorcentajeDescuento { get; set; }

    public decimal PorcentajeIva { get; set; }

    public decimal Subtotal => Dinero.Redondear(Cantidad * PrecioUnitario);

    public decimal ValorDescuento => Dinero.Porcentaje(Subtotal, PorcentajeDescuento);

    public decimal BaseGravable => Dinero.Redondear(Subtotal - ValorDescuento);

    public decimal ValorIva => Dinero.Porcentaje(BaseGravable, PorcentajeIva);

    public decimal Total => Dinero.Redondear(BaseGravable + ValorIva);
}

/// <summary>Cómo se cobra la cotización que el cliente aceptó.</summary>
public class SolicitudConversionCotizacion
{
    public MetodoPago MetodoPago { get; set; } = MetodoPago.Efectivo;

    public decimal MontoRecibido { get; set; }

    public string? ReferenciaPago { get; set; }

    public string? Observaciones { get; set; }
}

/// <summary>Datos con los que se registra una cotización.</summary>
public class SolicitudCotizacion
{
    public int ClienteId { get; set; }

    /// <summary>Días que se respetan los precios; si es cero, se usa el valor configurado.</summary>
    public int DiasValidez { get; set; }

    public string? Observaciones { get; set; }

    public List<LineaCotizacion> Lineas { get; set; } = new();

    public decimal Subtotal => Dinero.Redondear(Lineas.Sum(l => l.Subtotal));

    public decimal TotalDescuento => Dinero.Redondear(Lineas.Sum(l => l.ValorDescuento));

    public decimal TotalIva => Dinero.Redondear(Lineas.Sum(l => l.ValorIva));

    public decimal Total => Dinero.Redondear(Subtotal - TotalDescuento + TotalIva);
}
