namespace Papeleria.Business.Dtos;

/// <summary>Naturaleza del dato de una columna: define alineación y formato al exportar.</summary>
public enum TipoColumna
{
    Texto = 0,
    Entero = 1,
    Decimal = 2,
    Moneda = 3,
    Porcentaje = 4,
    Fecha = 5,
    FechaHora = 6,
    Booleano = 7
}

/// <summary>Definición de una columna del reporte.</summary>
public class ColumnaReporte
{
    public required string Titulo { get; init; }

    public TipoColumna Tipo { get; init; } = TipoColumna.Texto;

    /// <summary>Peso relativo usado para repartir el ancho en el PDF.</summary>
    public float Ancho { get; init; } = 1f;

    /// <summary>Los valores numéricos y de fecha se alinean a la derecha.</summary>
    public bool AlinearDerecha => Tipo is TipoColumna.Entero or TipoColumna.Decimal
        or TipoColumna.Moneda or TipoColumna.Porcentaje or TipoColumna.Fecha or TipoColumna.FechaHora;

    /// <summary>Indica si la columna debe totalizarse al pie.</summary>
    public bool Totalizar { get; init; }
}

/// <summary>Cifra destacada que acompaña al reporte (totales, promedios, conteos).</summary>
public record IndicadorReporte(string Etiqueta, string Valor);

/// <summary>
/// Representación neutra de cualquier reporte del sistema. Los servicios de negocio
/// la construyen y los exportadores la vuelcan a Excel, PDF o CSV sin conocer el dominio.
/// </summary>
public class ReporteTabular
{
    public required string Titulo { get; init; }

    public string? Subtitulo { get; init; }

    /// <summary>Descripción del periodo consultado, si aplica.</summary>
    public string? Periodo { get; init; }

    public DateTime GeneradoEn { get; init; } = DateTime.Now;

    public string GeneradoPor { get; init; } = string.Empty;

    public required IReadOnlyList<ColumnaReporte> Columnas { get; init; }

    /// <summary>Filas del reporte; cada arreglo sigue el orden de <see cref="Columnas"/>.</summary>
    public required IReadOnlyList<object?[]> Filas { get; init; }

    public IReadOnlyList<IndicadorReporte> Indicadores { get; init; } = Array.Empty<IndicadorReporte>();

    /// <summary>Mensaje mostrado cuando la consulta no arroja resultados.</summary>
    public string MensajeVacio { get; init; } = "No hay información para los criterios seleccionados.";

    public bool TieneDatos => Filas.Count > 0;

    /// <summary>Suma de una columna marcada como totalizable.</summary>
    public decimal TotalDeColumna(int indice)
    {
        decimal acumulado = 0;

        foreach (var fila in Filas)
        {
            if (indice < fila.Length && fila[indice] is { } valor)
            {
                acumulado += valor switch
                {
                    decimal d => d,
                    double db => (decimal)db,
                    int i => i,
                    long l => l,
                    _ => 0
                };
            }
        }

        return acumulado;
    }
}
