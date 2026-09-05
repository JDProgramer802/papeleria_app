using Papeleria.Business.Common;
using Papeleria.Business.Dtos;
using Papeleria.Domain.Enums;

namespace Papeleria.App.Impresion;

/// <summary>Par etiqueta/valor de las filas de totales de la tirilla.</summary>
public record FilaTicket(string Etiqueta, string Valor);

/// <summary>Renglón de la tirilla, ya con todo el texto armado.</summary>
public class LineaTicket
{
    public required string Descripcion { get; init; }

    public required string CantidadPorPrecio { get; init; }

    public required string SubtotalTexto { get; init; }

    public bool TieneDescuento { get; init; }

    public string DescuentoTexto { get; init; } = string.Empty;

    public string ValorDescuentoTexto { get; init; } = string.Empty;
}

/// <summary>
/// Lo que la tirilla necesita para dibujarse, ya masticado.
///
/// El formato de cada línea se resuelve aquí y no en el XAML a propósito: en una
/// impresora térmica el texto va en fuente de ancho fijo y lo que importa es la
/// cadena exacta, no un convertidor por cada dato.
/// </summary>
public class ReciboTicketDatos
{
    public required VentaDetalladaDto Venta { get; init; }

    public required DatosEmpresa Empresa { get; init; }

    public string FechaTexto => $"Fecha:   {Formatos.FechaHora(Venta.Fecha)}";

    public string CajeroTexto => $"Cajero:  {Venta.UsuarioNombre}";

    public string ClienteTexto => $"Cliente: {Venta.ClienteNombre}";

    public string DocumentoTexto => string.IsNullOrWhiteSpace(Venta.ClienteDocumento)
        ? string.Empty
        : $"Doc.:    {Venta.ClienteDocumento}";

    public bool EstaAnulada => Venta.Estado == EstadoVenta.Anulada;

    public string TotalTexto => Formatos.Moneda(Venta.Total);

    public string FormaPagoTexto => $"Forma de pago: {Venta.MetodoPago.Descripcion()}";

    public string ReferenciaTexto => Venta.TieneReferenciaPago
        ? $"Ref.: {Venta.ReferenciaPago}"
        : string.Empty;

    public string ArticulosTexto => $"Artículos: {Venta.CantidadArticulos}";

    public IReadOnlyList<LineaTicket> Lineas => Venta.Lineas.Select(l => new LineaTicket
    {
        Descripcion = l.Descripcion,
        CantidadPorPrecio = $"{Formatos.Cantidad(l.Cantidad)} x {Formatos.Moneda(l.ValorUnitario)}",
        SubtotalTexto = Formatos.Moneda(l.Subtotal),
        TieneDescuento = l.ValorDescuento > 0,
        DescuentoTexto = $"  Descuento {Formatos.Porcentaje(l.PorcentajeDescuento)}",
        ValorDescuentoTexto = $"-{Formatos.Moneda(l.ValorDescuento)}"
    }).ToList();

    public IReadOnlyList<FilaTicket> Totales
    {
        get
        {
            var filas = new List<FilaTicket> { new("Subtotal", Formatos.Moneda(Venta.Subtotal)) };

            if (Venta.TotalDescuento > 0)
            {
                filas.Add(new FilaTicket("Descuentos", $"-{Formatos.Moneda(Venta.TotalDescuento)}"));
            }

            if (Venta.TotalIva > 0)
            {
                filas.Add(new FilaTicket("IVA", Formatos.Moneda(Venta.TotalIva)));
            }

            return filas;
        }
    }

    /// <summary>Recibido y cambio; solo tienen sentido cuando entró efectivo.</summary>
    public IReadOnlyList<FilaTicket> Efectivo =>
        Venta.MetodoPago is MetodoPago.Efectivo or MetodoPago.Mixto
            ? new List<FilaTicket>
            {
                new("Recibido", Formatos.Moneda(Venta.MontoRecibido)),
                new("Cambio", Formatos.Moneda(Venta.Cambio))
            }
            : Array.Empty<FilaTicket>();
}
