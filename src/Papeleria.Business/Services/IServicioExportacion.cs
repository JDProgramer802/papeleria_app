using Papeleria.Business.Dtos;

namespace Papeleria.Business.Services;

/// <summary>Formatos disponibles para exportar un reporte.</summary>
public enum FormatoExportacion
{
    Excel = 0,
    Pdf = 1,
    Csv = 2
}

/// <summary>Vuelca un <see cref="ReporteTabular"/> a un archivo en el formato elegido.</summary>
public interface IServicioExportacion
{
    /// <summary>Extensión de archivo asociada al formato (incluye el punto).</summary>
    string ObtenerExtension(FormatoExportacion formato);

    /// <summary>Genera el archivo y devuelve la ruta creada.</summary>
    Task<string> ExportarAsync(
        ReporteTabular reporte, FormatoExportacion formato, string rutaDestino, CancellationToken ct = default);

    /// <summary>Sugiere un nombre de archivo a partir del título del reporte y la fecha.</summary>
    string SugerirNombreArchivo(ReporteTabular reporte, FormatoExportacion formato);
}
