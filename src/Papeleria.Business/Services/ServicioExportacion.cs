using System.Text;
using ClosedXML.Excel;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;
using Papeleria.Domain.Exceptions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Papeleria.Business.Services;

/// <inheritdoc cref="IServicioExportacion" />
public class ServicioExportacion : IServicioExportacion
{
    private const string ColorEncabezado = "#1565C0";
    private const string ColorFilaAlterna = "#F5F7FA";

    private readonly IServicioConfiguracion _configuracion;

    static ServicioExportacion()
    {
        // QuestPDF exige declarar la licencia antes de generar cualquier documento.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ServicioExportacion(IServicioConfiguracion configuracion) => _configuracion = configuracion;

    public string ObtenerExtension(FormatoExportacion formato) => formato switch
    {
        FormatoExportacion.Excel => ".xlsx",
        FormatoExportacion.Pdf => ".pdf",
        _ => ".csv"
    };

    public string SugerirNombreArchivo(ReporteTabular reporte, FormatoExportacion formato) =>
        Texto.NombreArchivoSeguro($"{reporte.Titulo}_{DateTime.Now:yyyyMMdd_HHmm}") +
        ObtenerExtension(formato);

    public async Task<string> ExportarAsync(
        ReporteTabular reporte, FormatoExportacion formato, string rutaDestino, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rutaDestino))
        {
            throw new NegocioException("Indique dónde desea guardar el archivo.");
        }

        var carpeta = Path.GetDirectoryName(rutaDestino);

        if (!string.IsNullOrWhiteSpace(carpeta))
        {
            Directory.CreateDirectory(carpeta);
        }

        try
        {
            switch (formato)
            {
                case FormatoExportacion.Excel:
                    await Task.Run(() => ExportarExcel(reporte, rutaDestino), ct).ConfigureAwait(false);
                    break;

                case FormatoExportacion.Pdf:
                    await Task.Run(() => ExportarPdf(reporte, rutaDestino), ct).ConfigureAwait(false);
                    break;

                default:
                    await ExportarCsvAsync(reporte, rutaDestino, ct).ConfigureAwait(false);
                    break;
            }
        }
        catch (IOException ex)
        {
            throw new NegocioException(
                "No se pudo escribir el archivo. Verifique que no esté abierto en otro programa.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new NegocioException(
                "No tiene permisos para escribir en la carpeta seleccionada.", ex);
        }

        return rutaDestino;
    }

    // ── Excel ───────────────────────────────────────────────────────────────

    private void ExportarExcel(ReporteTabular reporte, string ruta)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add(RecortarNombreHoja(reporte.Titulo));

        var totalColumnas = Math.Max(reporte.Columnas.Count, 1);
        var fila = 1;

        // Encabezado del documento
        hoja.Cell(fila, 1).Value = _configuracion.ObtenerEmpresa().Nombre;
        hoja.Range(fila, 1, fila, totalColumnas).Merge().Style
            .Font.SetBold().Font.SetFontSize(14)
            .Font.SetFontColor(XLColor.FromHtml(ColorEncabezado));
        fila++;

        hoja.Cell(fila, 1).Value = reporte.Titulo;
        hoja.Range(fila, 1, fila, totalColumnas).Merge().Style.Font.SetBold().Font.SetFontSize(12);
        fila++;

        foreach (var linea in ConstruirLineasDeContexto(reporte))
        {
            hoja.Cell(fila, 1).Value = linea;
            hoja.Range(fila, 1, fila, totalColumnas).Merge().Style
                .Font.SetFontSize(9).Font.SetFontColor(XLColor.Gray);
            fila++;
        }

        fila++;

        // Indicadores destacados
        if (reporte.Indicadores.Count > 0)
        {
            foreach (var indicador in reporte.Indicadores)
            {
                hoja.Cell(fila, 1).Value = indicador.Etiqueta;
                hoja.Cell(fila, 1).Style.Font.SetBold();
                hoja.Cell(fila, 2).Value = indicador.Valor;
                fila++;
            }

            fila++;
        }

        var filaEncabezado = fila;

        for (var i = 0; i < reporte.Columnas.Count; i++)
        {
            var celda = hoja.Cell(filaEncabezado, i + 1);
            celda.Value = reporte.Columnas[i].Titulo;
            celda.Style.Fill.SetBackgroundColor(XLColor.FromHtml(ColorEncabezado));
            celda.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
            celda.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            celda.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        }

        fila++;

        foreach (var registro in reporte.Filas)
        {
            for (var i = 0; i < reporte.Columnas.Count; i++)
            {
                var celda = hoja.Cell(fila, i + 1);
                var columna = reporte.Columnas[i];
                var valor = i < registro.Length ? registro[i] : null;

                AsignarValorExcel(celda, valor, columna);

                if ((fila - filaEncabezado) % 2 == 0)
                {
                    celda.Style.Fill.SetBackgroundColor(XLColor.FromHtml(ColorFilaAlterna));
                }
            }

            fila++;
        }

        // Fila de totales para las columnas marcadas
        if (reporte.TieneDatos && reporte.Columnas.Any(c => c.Totalizar))
        {
            hoja.Cell(fila, 1).Value = "TOTAL";
            hoja.Cell(fila, 1).Style.Font.SetBold();

            for (var i = 0; i < reporte.Columnas.Count; i++)
            {
                if (!reporte.Columnas[i].Totalizar)
                {
                    continue;
                }

                var celda = hoja.Cell(fila, i + 1);
                celda.Value = reporte.TotalDeColumna(i);
                celda.Style.NumberFormat.Format = FormatoNumericoExcel(reporte.Columnas[i].Tipo);
                celda.Style.Font.SetBold();
            }

            hoja.Range(fila, 1, fila, totalColumnas).Style.Border.SetTopBorder(XLBorderStyleValues.Medium);
        }

        if (reporte.TieneDatos)
        {
            hoja.Range(filaEncabezado, 1, filaEncabezado + reporte.Filas.Count, totalColumnas)
                .SetAutoFilter();
            hoja.SheetView.FreezeRows(filaEncabezado);
        }

        hoja.Columns().AdjustToContents(8d, 55d);

        libro.SaveAs(ruta);
    }

    private static void AsignarValorExcel(IXLCell celda, object? valor, ColumnaReporte columna)
    {
        if (valor is null)
        {
            celda.Value = string.Empty;
            return;
        }

        switch (columna.Tipo)
        {
            case TipoColumna.Entero:
            case TipoColumna.Decimal:
            case TipoColumna.Moneda:
            case TipoColumna.Porcentaje:
                celda.Value = Convert.ToDecimal(valor, Formatos.Cultura);
                celda.Style.NumberFormat.Format = FormatoNumericoExcel(columna.Tipo);
                break;

            case TipoColumna.Fecha when valor is DateTime fecha:
                celda.Value = fecha;
                celda.Style.DateFormat.Format = "dd/mm/yyyy";
                break;

            case TipoColumna.FechaHora when valor is DateTime fechaHora:
                celda.Value = fechaHora;
                celda.Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
                break;

            case TipoColumna.Booleano when valor is bool bandera:
                celda.Value = bandera ? "Sí" : "No";
                break;

            default:
                celda.Value = valor.ToString();
                break;
        }

        if (columna.AlinearDerecha)
        {
            celda.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
        }
    }

    private static string FormatoNumericoExcel(TipoColumna tipo) => tipo switch
    {
        TipoColumna.Moneda => "\"$\" #,##0",
        TipoColumna.Entero => "#,##0",
        TipoColumna.Porcentaje => "#,##0.0 \"%\"",
        _ => "#,##0.00"
    };

    private static string RecortarNombreHoja(string titulo)
    {
        // Excel limita el nombre de hoja a 31 caracteres y prohíbe ciertos símbolos.
        var limpio = new string(titulo.Where(c => !"[]:*?/\\".Contains(c)).ToArray()).Trim();
        return limpio.Length <= 31 ? limpio : limpio[..31];
    }

    // ── PDF ─────────────────────────────────────────────────────────────────

    private void ExportarPdf(ReporteTabular reporte, string ruta)
    {
        var empresa = _configuracion.ObtenerEmpresa();
        var esAncho = reporte.Columnas.Count > 6;

        Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(esAncho ? PageSizes.A4.Landscape() : PageSizes.A4);
                pagina.Margin(24);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(8.5f).FontFamily(Fonts.Calibri));

                pagina.Header().Element(contenedor => ComponerEncabezadoPdf(contenedor, reporte, empresa.Nombre));
                pagina.Content().PaddingVertical(8).Element(contenedor => ComponerTablaPdf(contenedor, reporte));

                pagina.Footer().Row(fila =>
                {
                    fila.RelativeItem().Text($"Generado el {Formatos.FechaHora(reporte.GeneradoEn)}")
                        .FontSize(7).FontColor(Colors.Grey.Darken1);

                    fila.RelativeItem().AlignRight().Text(texto =>
                    {
                        texto.DefaultTextStyle(estilo => estilo.FontSize(7).FontColor(Colors.Grey.Darken1));
                        texto.Span("Página ");
                        texto.CurrentPageNumber();
                        texto.Span(" de ");
                        texto.TotalPages();
                    });
                });
            });
        }).GeneratePdf(ruta);
    }

    private void ComponerEncabezadoPdf(IContainer contenedor, ReporteTabular reporte, string nombreEmpresa)
    {
        contenedor.Column(columna =>
        {
            columna.Item().Text(nombreEmpresa).FontSize(15).Bold().FontColor(ColorEncabezado);
            columna.Item().Text(reporte.Titulo).FontSize(12).SemiBold();

            foreach (var linea in ConstruirLineasDeContexto(reporte))
            {
                columna.Item().Text(linea).FontSize(8).FontColor(Colors.Grey.Darken1);
            }

            if (reporte.Indicadores.Count > 0)
            {
                columna.Item().PaddingTop(6).Row(fila =>
                {
                    foreach (var indicador in reporte.Indicadores)
                    {
                        fila.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2)
                            .Background(Colors.Grey.Lighten5).Padding(5).Column(tarjeta =>
                            {
                                tarjeta.Item().Text(indicador.Etiqueta)
                                    .FontSize(7).FontColor(Colors.Grey.Darken1);
                                tarjeta.Item().Text(indicador.Valor).FontSize(10).Bold();
                            });
                    }
                });
            }

            columna.Item().PaddingTop(6).LineHorizontal(1).LineColor(ColorEncabezado);
        });
    }

    private void ComponerTablaPdf(IContainer contenedor, ReporteTabular reporte)
    {
        if (!reporte.TieneDatos)
        {
            contenedor.PaddingTop(40).AlignCenter()
                .Text(reporte.MensajeVacio).FontSize(10).FontColor(Colors.Grey.Darken1);
            return;
        }

        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(definicion =>
            {
                foreach (var columna in reporte.Columnas)
                {
                    definicion.RelativeColumn(columna.Ancho);
                }
            });

            tabla.Header(encabezado =>
            {
                foreach (var columna in reporte.Columnas)
                {
                    var celda = encabezado.Cell().Background(ColorEncabezado).Padding(4);
                    var texto = celda.Text(columna.Titulo).FontColor(Colors.White).Bold().FontSize(8);

                    if (columna.AlinearDerecha)
                    {
                        texto.AlignRight();
                    }
                }
            });

            var indiceFila = 0;

            foreach (var registro in reporte.Filas)
            {
                Color fondo = indiceFila % 2 == 0 ? Colors.White : ColorFilaAlterna;

                for (var i = 0; i < reporte.Columnas.Count; i++)
                {
                    var columna = reporte.Columnas[i];
                    var valor = i < registro.Length ? registro[i] : null;

                    var celda = tabla.Cell().Background(fondo)
                        .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                        .PaddingVertical(3).PaddingHorizontal(4);

                    var texto = celda.Text(Formatos.ValorDeColumna(valor, columna.Tipo)).FontSize(8);

                    if (columna.AlinearDerecha)
                    {
                        texto.AlignRight();
                    }
                }

                indiceFila++;
            }

            if (reporte.Columnas.Any(c => c.Totalizar))
            {
                for (var i = 0; i < reporte.Columnas.Count; i++)
                {
                    var columna = reporte.Columnas[i];

                    var celda = tabla.Cell().Background(Colors.Grey.Lighten3)
                        .BorderTop(1).BorderColor(Colors.Grey.Darken1)
                        .PaddingVertical(4).PaddingHorizontal(4);

                    if (i == 0)
                    {
                        celda.Text("TOTAL").Bold().FontSize(8);
                        continue;
                    }

                    if (!columna.Totalizar)
                    {
                        celda.Text(string.Empty);
                        continue;
                    }

                    celda.Text(Formatos.ValorDeColumna(reporte.TotalDeColumna(i), columna.Tipo))
                        .Bold().FontSize(8).AlignRight();
                }
            }
        });
    }

    // ── CSV ─────────────────────────────────────────────────────────────────

    private static async Task ExportarCsvAsync(ReporteTabular reporte, string ruta, CancellationToken ct)
    {
        var constructor = new StringBuilder();

        constructor.AppendLine(EscaparCsv(reporte.Titulo));

        foreach (var linea in ConstruirLineasDeContexto(reporte))
        {
            constructor.AppendLine(EscaparCsv(linea));
        }

        constructor.AppendLine();
        constructor.AppendLine(string.Join(';', reporte.Columnas.Select(c => EscaparCsv(c.Titulo))));

        foreach (var registro in reporte.Filas)
        {
            var celdas = new List<string>(reporte.Columnas.Count);

            for (var i = 0; i < reporte.Columnas.Count; i++)
            {
                var valor = i < registro.Length ? registro[i] : null;
                celdas.Add(EscaparCsv(Formatos.ValorDeColumna(valor, reporte.Columnas[i].Tipo)));
            }

            constructor.AppendLine(string.Join(';', celdas));
        }

        // BOM UTF-8 para que Excel reconozca las tildes al abrir el archivo directamente.
        await File.WriteAllTextAsync(ruta, constructor.ToString(), new UTF8Encoding(true), ct)
            .ConfigureAwait(false);
    }

    private static string EscaparCsv(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
        {
            return string.Empty;
        }

        var necesitaComillas = valor.Contains(';') || valor.Contains('"') ||
                               valor.Contains('\n') || valor.Contains('\r');

        return necesitaComillas ? $"\"{valor.Replace("\"", "\"\"")}\"" : valor;
    }

    private static IEnumerable<string> ConstruirLineasDeContexto(ReporteTabular reporte)
    {
        if (!string.IsNullOrWhiteSpace(reporte.Subtitulo))
        {
            yield return reporte.Subtitulo;
        }

        if (!string.IsNullOrWhiteSpace(reporte.Periodo))
        {
            yield return $"Periodo: {reporte.Periodo}";
        }

        // Si el reporte salió recortado, el archivo exportado tiene que decirlo:
        // de lo contrario parecería completo fuera de la aplicación.
        if (reporte.TieneAdvertencia)
        {
            yield return $"AVISO: {reporte.Advertencia}";
        }

        var generado = $"Generado el {Formatos.FechaHora(reporte.GeneradoEn)}";

        yield return string.IsNullOrWhiteSpace(reporte.GeneradoPor)
            ? generado
            : $"{generado} por {reporte.GeneradoPor}";
    }
}
