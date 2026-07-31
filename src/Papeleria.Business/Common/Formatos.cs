using System.Globalization;
using Papeleria.Business.Dtos;

namespace Papeleria.Business.Common;

/// <summary>
/// Formato uniforme de números, importes y fechas en todo el sistema.
/// La cultura es es-CO: separador de miles «.» y decimal «,», como espera un usuario colombiano.
/// </summary>
public static class Formatos
{
    public static CultureInfo Cultura { get; } = CrearCultura();

    private static CultureInfo CrearCultura()
    {
        var cultura = (CultureInfo)CultureInfo.GetCultureInfo("es-CO").Clone();

        // El peso colombiano se maneja habitualmente sin decimales en el mostrador.
        cultura.NumberFormat.CurrencySymbol = "$";
        cultura.NumberFormat.CurrencyDecimalDigits = 0;
        cultura.NumberFormat.CurrencyPositivePattern = 2; // "$ 1.234"
        cultura.NumberFormat.CurrencyNegativePattern = 9; // "$ -1.234"

        return cultura;
    }

    public static string Moneda(decimal valor, int decimales = 0) =>
        valor.ToString($"C{decimales}", Cultura);

    public static string Numero(decimal valor, int decimales = 2) =>
        valor.ToString($"N{decimales}", Cultura);

    public static string Entero(decimal valor) => valor.ToString("N0", Cultura);

    /// <summary>Muestra las cantidades sin decimales cuando son enteras.</summary>
    public static string Cantidad(decimal valor) =>
        valor == Math.Truncate(valor) ? valor.ToString("N0", Cultura) : valor.ToString("N2", Cultura);

    public static string Porcentaje(decimal valor, int decimales = 1) =>
        $"{valor.ToString($"N{decimales}", Cultura)} %";

    public static string Fecha(DateTime valor) => valor.ToString("dd/MM/yyyy", Cultura);

    public static string FechaHora(DateTime valor) => valor.ToString("dd/MM/yyyy hh:mm tt", Cultura);

    public static string Hora(DateTime valor) => valor.ToString("hh:mm tt", Cultura);

    /// <summary>Fecha larga usada en encabezados: «29 de julio de 2026».</summary>
    public static string FechaLarga(DateTime valor) => valor.ToString("dd 'de' MMMM 'de' yyyy", Cultura);

    /// <summary>Convierte un valor de reporte al texto que corresponde a su tipo de columna.</summary>
    public static string ValorDeColumna(object? valor, TipoColumna tipo)
    {
        if (valor is null)
        {
            return string.Empty;
        }

        return tipo switch
        {
            TipoColumna.Moneda => Moneda(AsDecimal(valor)),
            TipoColumna.Decimal => Numero(AsDecimal(valor)),
            TipoColumna.Entero => Entero(AsDecimal(valor)),
            TipoColumna.Porcentaje => Porcentaje(AsDecimal(valor)),
            TipoColumna.Fecha when valor is DateTime fecha => Fecha(fecha),
            TipoColumna.FechaHora when valor is DateTime fechaHora => FechaHora(fechaHora),
            TipoColumna.Booleano when valor is bool bandera => bandera ? "Sí" : "No",
            _ => valor.ToString() ?? string.Empty
        };
    }

    private static decimal AsDecimal(object valor) => valor switch
    {
        decimal d => d,
        double db => (decimal)db,
        float f => (decimal)f,
        int i => i,
        long l => l,
        _ => decimal.TryParse(valor.ToString(), NumberStyles.Any, Cultura, out var resultado) ? resultado : 0
    };
}
