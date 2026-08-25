using System.IO;
using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Papeleria.App.Infrastructure;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;
using Papeleria.Business.Services;
using Papeleria.Business.Services.Catalogos;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Entities;

namespace Papeleria.App.ViewModels.Paginas;

/// <summary>
/// Centro de informes: genera cualquiera de los reportes del sistema, muestra
/// una vista previa en pantalla y lo exporta a Excel, PDF o CSV.
/// </summary>
public partial class ReportesVistaModelo : PaginaVistaModelo
{
    private readonly IServicioReportes _reportes;
    private readonly IServicioExportacion _exportacion;
    private readonly IServicioCategorias _categorias;
    private readonly IServicioArchivos _archivos;
    private readonly IServicioDialogos _dialogos;

    public ReportesVistaModelo(
        IServicioReportes reportes,
        IServicioExportacion exportacion,
        IServicioCategorias categorias,
        IServicioArchivos archivos,
        IServicioDialogos dialogos)
    {
        _reportes = reportes;
        _exportacion = exportacion;
        _categorias = categorias;
        _archivos = archivos;
        _dialogos = dialogos;

        Titulo = "Reportes";
        Subtitulo = "Informes del negocio con exportación a Excel, PDF y CSV";

        Catalogo = new ObservableCollection<DefinicionReporte>(_reportes.Catalogo);
        ReporteSeleccionado = Catalogo.FirstOrDefault();
    }

    public override string Modulo => Modulos.Reportes;

    public ObservableCollection<DefinicionReporte> Catalogo { get; }

    public ObservableCollection<Categoria> Categorias { get; } = new();

    /// <summary>Tabla de vista previa construida a partir del reporte generado.</summary>
    [ObservableProperty]
    private DataView? _vistaPrevia;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayAdvertencia))]
    [NotifyPropertyChangedFor(nameof(TextoAdvertencia))]
    [NotifyPropertyChangedFor(nameof(SinGenerar))]
    private ReporteTabular? _reporte;

    [ObservableProperty]
    private DefinicionReporte? _reporteSeleccionado;

    [ObservableProperty] private DateTime _desde = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime _hasta = DateTime.Today;
    [ObservableProperty] private int? _categoriaId;

    public bool RequierePeriodo => ReporteSeleccionado?.RequierePeriodo ?? false;

    public bool HayResultado => Reporte is { TieneDatos: true };

    /// <summary>
    /// Sin reporte generado el enlace a Reporte.TieneAdvertencia falla y el aviso se
    /// quedaba visible en blanco. Aquí la condición es explícita.
    /// </summary>
    public bool HayAdvertencia => Reporte is { TieneAdvertencia: true };

    public string TextoAdvertencia => Reporte?.Advertencia ?? string.Empty;

    /// <summary>Todavía no se ha generado nada: la vista previa muestra la invitación.</summary>
    public bool SinGenerar => Reporte is null;

    public bool SinResultado => Reporte is { TieneDatos: false };

    partial void OnReporteSeleccionadoChanged(DefinicionReporte? value)
    {
        OnPropertyChanged(nameof(RequierePeriodo));
        Reporte = null;
        VistaPrevia = null;
        OnPropertyChanged(nameof(HayResultado));
        OnPropertyChanged(nameof(SinResultado));
    }

    public override async Task CargarAsync()
    {
        await CargarCategoriasAsync().ConfigureAwait(true);

        if (Reporte is null)
        {
            await GenerarAsync().ConfigureAwait(true);
        }
    }

    private Task CargarCategoriasAsync() => EjecutarAsync(async () =>
    {
        if (Categorias.Count > 0)
        {
            return;
        }

        var categorias = await _categorias.ListarAsync().ConfigureAwait(true);

        Categorias.Clear();
        Categorias.Add(new Categoria { Id = 0, Nombre = "Todas las categorías" });

        foreach (var categoria in categorias)
        {
            Categorias.Add(categoria);
        }
    }, "No se pudieron cargar las categorías.");

    [RelayCommand]
    private Task GenerarAsync() => EjecutarAsync(async () =>
    {
        if (ReporteSeleccionado is null)
        {
            return;
        }

        if (Desde > Hasta)
        {
            MensajeError = "La fecha inicial no puede ser posterior a la final.";
            return;
        }

        var reporte = await _reportes.GenerarAsync(new ParametrosReporte
        {
            Tipo = ReporteSeleccionado.Tipo,
            Desde = Desde,
            Hasta = Hasta,
            CategoriaId = CategoriaId
        }).ConfigureAwait(true);

        Reporte = reporte;
        VistaPrevia = ConstruirTabla(reporte);

        OnPropertyChanged(nameof(HayResultado));
        OnPropertyChanged(nameof(SinResultado));
    }, "No se pudo generar el reporte.");

    /// <summary>
    /// Convierte el reporte en un <see cref="DataTable"/> ya formateado como texto,
    /// que la grilla puede mostrar con columnas dinámicas sin conocer el reporte.
    /// </summary>
    private static DataView ConstruirTabla(ReporteTabular reporte)
    {
        var tabla = new DataTable(reporte.Titulo);

        foreach (var columna in reporte.Columnas)
        {
            // Los nombres se hacen únicos porque DataTable no admite duplicados.
            var nombre = columna.Titulo;
            var sufijo = 1;

            while (tabla.Columns.Contains(nombre))
            {
                nombre = $"{columna.Titulo} ({++sufijo})";
            }

            tabla.Columns.Add(nombre, typeof(string));
        }

        foreach (var registro in reporte.Filas)
        {
            var fila = tabla.NewRow();

            for (var i = 0; i < reporte.Columnas.Count; i++)
            {
                var valor = i < registro.Length ? registro[i] : null;
                fila[i] = Formatos.ValorDeColumna(valor, reporte.Columnas[i].Tipo);
            }

            tabla.Rows.Add(fila);
        }

        return tabla.DefaultView;
    }

    [RelayCommand]
    private Task ExportarExcelAsync() => ExportarAsync(FormatoExportacion.Excel);

    [RelayCommand]
    private Task ExportarPdfAsync() => ExportarAsync(FormatoExportacion.Pdf);

    [RelayCommand]
    private Task ExportarCsvAsync() => ExportarAsync(FormatoExportacion.Csv);

    /// <summary>Genera el PDF y lo abre; desde el visor el usuario puede imprimirlo.</summary>
    [RelayCommand]
    private Task ImprimirAsync() => EjecutarAsync(async () =>
    {
        if (Reporte is null)
        {
            return;
        }

        var ruta = Data.Storage.RutasAplicacion.RutaTemporal(".pdf");

        await _exportacion.ExportarAsync(Reporte, FormatoExportacion.Pdf, ruta).ConfigureAwait(true);

        _archivos.AbrirConAplicacionPredeterminada(ruta);
    }, "No se pudo preparar el reporte para imprimir.");

    private Task ExportarAsync(FormatoExportacion formato) => EjecutarAsync(async () =>
    {
        if (Reporte is null)
        {
            return;
        }

        var extension = _exportacion.ObtenerExtension(formato);

        var ruta = _archivos.SeleccionarDondeGuardar(
            "Exportar reporte",
            $"Archivo {extension.TrimStart('.').ToUpperInvariant()}|*{extension}",
            _exportacion.SugerirNombreArchivo(Reporte, formato));

        if (string.IsNullOrWhiteSpace(ruta))
        {
            return;
        }

        await _exportacion.ExportarAsync(Reporte, formato, ruta).ConfigureAwait(true);

        _dialogos.Notificar($"Reporte exportado: {Path.GetFileName(ruta)}");
        _archivos.AbrirConAplicacionPredeterminada(ruta);
    }, "No se pudo exportar el reporte.");

    [RelayCommand]
    private void PeriodoMesActual()
    {
        var hoy = DateTime.Today;
        Desde = new DateTime(hoy.Year, hoy.Month, 1);
        Hasta = hoy;
    }

    [RelayCommand]
    private void PeriodoMesAnterior()
    {
        var inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
        Desde = inicio;
        Hasta = inicio.AddMonths(1).AddDays(-1);
    }

    [RelayCommand]
    private void PeriodoAnioActual()
    {
        Desde = new DateTime(DateTime.Today.Year, 1, 1);
        Hasta = DateTime.Today;
    }
}
