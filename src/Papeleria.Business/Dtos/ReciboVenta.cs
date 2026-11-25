using Papeleria.Business.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Business.Dtos;

/// <summary>Par etiqueta/valor de una fila del comprobante.</summary>
public record FilaRecibo(string Etiqueta, string Valor);

/// <summary>Renglón de producto del comprobante, con todo el texto ya armado.</summary>
public record LineaRecibo(
    string Descripcion,
    string CantidadPorPrecio,
    string Valor,
    string MarcaIva,
    string? Descuento,
    string? ValorDescuento)
{
    public bool TieneDescuento => Descuento is not null;
}

/// <summary>Una fila de la discriminación del IVA: una por cada tarifa vendida.</summary>
public record ImpuestoRecibo(string Marca, string Tarifa, string Base, string Iva);

/// <summary>
/// Todo lo que lleva impreso una factura de venta, ya redactado.
///
/// La tirilla térmica (que dibuja WPF) y el PDF (que dibuja QuestPDF) salen de aquí
/// los dos. Antes cada uno armaba sus propios textos y era cuestión de tiempo que
/// uno dijera una cosa y el otro otra; una factura no puede tener dos versiones.
/// </summary>
public class ReciboVenta
{
    /// <summary>Firma del programa al pie: la DIAN pide que el soporte diga con qué se hizo.</summary>
    public const string NombreSoftware = "PapelSoft";

    public ReciboVenta(VentaDetalladaDto venta, DatosEmpresa empresa, bool esCopia = false, DateTime? impresoEl = null)
    {
        Venta = venta;
        Empresa = empresa;
        EsCopia = esCopia;
        ImpresoEl = impresoEl ?? DateTime.Now;

        Impuestos = ArmarImpuestos(out var marcas);
        Lineas = ArmarLineas(marcas);
    }

    public VentaDetalladaDto Venta { get; }

    public DatosEmpresa Empresa { get; }

    /// <summary>Reimpresión desde el historial: el original ya se le entregó al cliente.</summary>
    public bool EsCopia { get; }

    public DateTime ImpresoEl { get; }

    // ── Encabezado ──────────────────────────────────────────────────────────

    /// <summary>Renglones bajo el nombre: NIT, régimen, dirección y contacto.</summary>
    public IReadOnlyList<string> Encabezado =>
        new[] { Empresa.LineaIdentificacion, Empresa.Regimen, Empresa.LineaUbicacion, Empresa.LineaContacto }
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

    public string Titulo => string.IsNullOrWhiteSpace(Empresa.TituloFactura)
        ? "FACTURA DE VENTA"
        : Empresa.TituloFactura.Trim().ToUpper(Formatos.Cultura);

    public string Numero => $"N.º {Venta.NumeroFactura}";

    public bool EsAnulada => Venta.Estado == EstadoVenta.Anulada;

    public string? MotivoAnulacion => EsAnulada ? Venta.MotivoAnulacion : null;

    /// <summary>Fecha, hora, quién cobró y a quién se le vendió.</summary>
    public IReadOnlyList<FilaRecibo> DatosVenta
    {
        get
        {
            var filas = new List<FilaRecibo>
            {
                new("Fecha", Venta.Fecha.ToString("yyyy-MM-dd", Formatos.Cultura)),
                new("Hora", Formatos.Hora(Venta.Fecha)),
                new("Cajero", Venta.UsuarioNombre),
                new("Cliente", Venta.ClienteNombre)
            };

            Agregar(filas, "C.C./NIT", Venta.ClienteDocumento);
            Agregar(filas, "Teléfono", Venta.ClienteTelefono);
            Agregar(filas, "Dirección", Venta.ClienteDireccion);

            return filas;
        }
    }

    // ── Detalle ─────────────────────────────────────────────────────────────

    public IReadOnlyList<LineaRecibo> Lineas { get; }

    public string ResumenArticulos =>
        $"Artículos: {Venta.CantidadArticulos}   Renglones: {Venta.Lineas.Count}";

    // ── Totales ─────────────────────────────────────────────────────────────

    /// <summary>De la suma bruta al valor sobre el que se cobra el impuesto.</summary>
    public IReadOnlyList<FilaRecibo> Totales
    {
        get
        {
            var filas = new List<FilaRecibo> { new("Subtotal", Formatos.Moneda(Venta.Subtotal)) };

            if (Venta.TotalDescuento > 0)
            {
                filas.Add(new FilaRecibo("Descuentos", $"-{Formatos.Moneda(Venta.TotalDescuento)}"));
            }

            if (DiscriminaIva)
            {
                filas.Add(new FilaRecibo("Base gravable", Formatos.Moneda(Venta.Subtotal - Venta.TotalDescuento)));
                filas.Add(new FilaRecibo("IVA", Formatos.Moneda(Venta.TotalIva)));
            }

            return filas;
        }
    }

    public string TotalTexto => Formatos.Moneda(Venta.Total);

    public string TotalEnLetras => $"SON: {NumeroEnLetras.Pesos(Venta.Total)}";

    /// <summary>
    /// Solo se discrimina el IVA cuando hubo IVA. A un no responsable de IVA le
    /// sobraría un cuadro lleno de ceros, y la ley tampoco se lo pide.
    /// </summary>
    public bool DiscriminaIva => Venta.TotalIva > 0;

    public IReadOnlyList<ImpuestoRecibo> Impuestos { get; }

    // ── Pago ────────────────────────────────────────────────────────────────

    public IReadOnlyList<FilaRecibo> Pago
    {
        get
        {
            var filas = new List<FilaRecibo> { new("Forma de pago", Venta.MetodoPago.Descripcion()) };

            // La referencia va impresa: es la que el cliente reclama cuando dice que
            // ya pagó y hay que buscar el movimiento.
            if (Venta.TieneReferenciaPago)
            {
                filas.Add(new FilaRecibo("Referencia", Venta.ReferenciaPago!));
            }

            if (Venta.MetodoPago is MetodoPago.Efectivo or MetodoPago.Mixto)
            {
                filas.Add(new FilaRecibo("Recibido", Formatos.Moneda(Venta.MontoRecibido)));
                filas.Add(new FilaRecibo("Cambio", Formatos.Moneda(Venta.Cambio)));
            }

            return filas;
        }
    }

    // ── Pie ─────────────────────────────────────────────────────────────────

    public IReadOnlyList<string> Resolucion => Empresa.LineasResolucion;

    public string Pie => Empresa.PieFactura;

    public string LineaCopia => $"COPIA — reimpresa el {ImpresoEl.ToString("yyyy-MM-dd hh:mm tt", Formatos.Cultura)}";

    public string LineaSoftware => $"Software: {NombreSoftware}";

    // ── Armado ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Una fila por tarifa, de la más alta a la más baja, cada una con su letra. La
    /// letra es la que va al lado de cada renglón, como en cualquier caja de almacén:
    /// así se ve de un vistazo qué pagó IVA y qué no.
    /// </summary>
    private IReadOnlyList<ImpuestoRecibo> ArmarImpuestos(out Dictionary<decimal, string> marcas)
    {
        marcas = new Dictionary<decimal, string>();

        if (!DiscriminaIva)
        {
            return Array.Empty<ImpuestoRecibo>();
        }

        var grupos = Venta.Lineas
            .GroupBy(l => l.PorcentajeIva)
            .OrderByDescending(g => g.Key)
            .ToList();

        var filas = new List<ImpuestoRecibo>();
        var letra = 'A';

        foreach (var grupo in grupos)
        {
            var marca = letra.ToString();
            marcas[grupo.Key] = marca;
            letra++;

            var baseGravable = grupo.Sum(l => l.Subtotal - l.ValorDescuento);

            filas.Add(new ImpuestoRecibo(
                marca,
                grupo.Key == 0 ? "Excluido" : $"IVA {Formatos.Porcentaje(grupo.Key, grupo.Key % 1 == 0 ? 0 : 1)}",
                Formatos.Moneda(baseGravable),
                Formatos.Moneda(grupo.Sum(l => l.ValorIva))));
        }

        return filas;
    }

    private IReadOnlyList<LineaRecibo> ArmarLineas(IReadOnlyDictionary<decimal, string> marcas) =>
        Venta.Lineas.Select(l =>
        {
            var unidad = string.IsNullOrWhiteSpace(l.UnidadAbreviatura) ? string.Empty : $" {l.UnidadAbreviatura}";
            var tieneDescuento = l.ValorDescuento > 0;

            return new LineaRecibo(
                l.Descripcion,
                $"{Formatos.Cantidad(l.Cantidad)}{unidad} x {Formatos.Moneda(l.ValorUnitario)}",
                Formatos.Moneda(l.Subtotal),
                marcas.TryGetValue(l.PorcentajeIva, out var marca) ? marca : string.Empty,
                tieneDescuento ? $"  Descuento {Formatos.Porcentaje(l.PorcentajeDescuento)}" : null,
                tieneDescuento ? $"-{Formatos.Moneda(l.ValorDescuento)}" : null);
        }).ToList();

    private static void Agregar(List<FilaRecibo> filas, string etiqueta, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor))
        {
            filas.Add(new FilaRecibo(etiqueta, valor.Trim()));
        }
    }
}
