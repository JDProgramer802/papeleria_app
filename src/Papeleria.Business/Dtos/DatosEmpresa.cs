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

    public string Resolucion { get; set; } = string.Empty;

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
}
