namespace Papeleria.Business.Dtos;

/// <summary>Datos de la empresa que encabezan facturas, etiquetas y reportes.</summary>
public class DatosEmpresa
{
    public string Nombre { get; set; } = "Mi Papelería";

    public string Nit { get; set; } = string.Empty;

    public string Direccion { get; set; } = string.Empty;

    public string Telefono { get; set; } = string.Empty;

    public string Correo { get; set; } = string.Empty;

    public string Ciudad { get; set; } = string.Empty;

    public string Eslogan { get; set; } = string.Empty;

    /// <summary>Ruta absoluta del logo dentro del almacén local, si se cargó uno.</summary>
    public string LogoPath { get; set; } = string.Empty;

    /// <summary>Responsabilidad frente al IVA, tal como figura en el RUT.</summary>
    public string Regimen { get; set; } = string.Empty;

    /// <summary>Leyenda libre de la resolución; se imprime además de la autorización por partes.</summary>
    public string Resolucion { get; set; } = string.Empty;

    /// <summary>Nombre del documento: «Factura de venta», «Documento equivalente POS»…</summary>
    public string TituloFactura { get; set; } = "Factura de venta";

    public string ResolucionNumero { get; set; } = string.Empty;

    public DateTime? ResolucionFecha { get; set; }

    public int? ResolucionDesde { get; set; }

    public int? ResolucionHasta { get; set; }

    /// <summary>Último día en que la autorización de numeración sirve.</summary>
    public DateTime? ResolucionVigencia { get; set; }

    /// <summary>Prefijo del consecutivo, el mismo que autorizó la DIAN.</summary>
    public string PrefijoFactura { get; set; } = string.Empty;

    public string PieFactura { get; set; } = string.Empty;

    public string MonedaSimbolo { get; set; } = "$";

    public string MonedaCodigo { get; set; } = "COP";

    public int DecimalesMoneda { get; set; }

    public decimal IvaPorDefecto { get; set; } = 19m;

    public bool TieneLogo => !string.IsNullOrWhiteSpace(LogoPath) && File.Exists(LogoPath);

    /// <summary>Línea de identificación usada en encabezados: «NIT 900.123.456-7».</summary>
    public string LineaIdentificacion => string.IsNullOrWhiteSpace(Nit) ? string.Empty : $"NIT {Nit}";

    /// <summary>Dirección y ciudad concatenadas para el encabezado del recibo.</summary>
    public string LineaUbicacion =>
        string.Join(" · ", new[] { Direccion, Ciudad }.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>Teléfono y correo concatenados.</summary>
    public string LineaContacto =>
        string.Join(" · ", new[] { Telefono, Correo }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public bool TieneResolucionDian => !string.IsNullOrWhiteSpace(ResolucionNumero);

    /// <summary>
    /// La autorización de numeración redactada como la pide la DIAN, una frase por
    /// renglón: número y fecha, prefijo y rango, vigencia. La leyenda libre, si la
    /// hay, va al final.
    /// </summary>
    public IReadOnlyList<string> LineasResolucion
    {
        get
        {
            var lineas = new List<string>();

            if (TieneResolucionDian)
            {
                lineas.Add(ResolucionFecha is { } fecha
                    ? $"Resolución DIAN N.º {ResolucionNumero} del {fecha:yyyy-MM-dd}"
                    : $"Resolución DIAN N.º {ResolucionNumero}");

                if (ResolucionDesde is { } desde && ResolucionHasta is { } hasta)
                {
                    var prefijo = PrefijoFactura.Trim().TrimEnd('-', ' ');

                    lineas.Add(string.IsNullOrEmpty(prefijo)
                        ? $"Numeración autorizada del {desde} al {hasta}"
                        : $"Prefijo {prefijo} del {desde} al {hasta}");
                }

                if (ResolucionVigencia is { } vigencia)
                {
                    lineas.Add($"Vigencia hasta {vigencia:yyyy-MM-dd}");
                }
            }

            if (!string.IsNullOrWhiteSpace(Resolucion))
            {
                lineas.Add(Resolucion.Trim());
            }

            return lineas;
        }
    }
}
