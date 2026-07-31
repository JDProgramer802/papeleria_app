using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Papeleria.App.Infrastructure;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;
using Papeleria.Business.Services;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Enums;

namespace Papeleria.App.ViewModels.Paginas;

/// <summary>
/// Consulta del kardex. Es de solo lectura por diseño: los movimientos son
/// inmutables tanto en la aplicación como en la base de datos.
/// </summary>
public partial class KardexVistaModelo : PaginaVistaModelo
{
    private readonly IServicioKardex _kardex;
    private readonly IServicioReportes _reportes;
    private readonly IServicioExportacion _exportacion;
    private readonly IServicioArchivos _archivos;
    private readonly IServicioDialogos _dialogos;

    public KardexVistaModelo(
        IServicioKardex kardex,
        IServicioReportes reportes,
        IServicioExportacion exportacion,
        IServicioArchivos archivos,
        IServicioDialogos dialogos)
    {
        _kardex = kardex;
        _reportes = reportes;
        _exportacion = exportacion;
        _archivos = archivos;
        _dialogos = dialogos;

        Titulo = "Kardex";
        Subtitulo = "Historial inalterable de todos los movimientos de inventario";

        Paginador.PaginaCambiada += (_, _) => _ = BuscarAsync();

        TiposMovimiento = new List<KeyValuePair<TipoMovimientoKardex?, string>> { new(null, "Todos los movimientos") };

        foreach (var opcion in Enumeraciones.Opciones<TipoMovimientoKardex>())
        {
            TiposMovimiento.Add(new KeyValuePair<TipoMovimientoKardex?, string>(opcion.Valor, opcion.Descripcion));
        }
    }

    public override string Modulo => Modulos.Kardex;

    public Paginador Paginador { get; } = new();

    public ObservableCollection<MovimientoKardexDto> Movimientos { get; } = new();

    public List<KeyValuePair<TipoMovimientoKardex?, string>> TiposMovimiento { get; }

    [ObservableProperty] private string? _textoBusqueda;
    [ObservableProperty] private DateTime? _desde = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime? _hasta = DateTime.Today;
    [ObservableProperty] private TipoMovimientoKardex? _tipoSeleccionado;

    partial void OnTextoBusquedaChanged(string? value) => ReiniciarBusqueda();
    partial void OnDesdeChanged(DateTime? value) => ReiniciarBusqueda();
    partial void OnHastaChanged(DateTime? value) => ReiniciarBusqueda();
    partial void OnTipoSeleccionadoChanged(TipoMovimientoKardex? value) => ReiniciarBusqueda();

    private void ReiniciarBusqueda()
    {
        Paginador.Reiniciar();
        _ = BuscarAsync();
    }

    public override Task CargarAsync() => BuscarAsync();

    [RelayCommand]
    private Task BuscarAsync() => EjecutarAsync(async () =>
    {
        var resultado = await _kardex.BuscarAsync(new FiltroKardex
        {
            Texto = TextoBusqueda,
            Desde = Desde,
            Hasta = Hasta,
            Tipo = TipoSeleccionado,
            Pagina = Paginador.Pagina,
            TamanoPagina = Paginador.TamanoPagina
        }).ConfigureAwait(true);

        Movimientos.Clear();

        foreach (var movimiento in resultado.Elementos)
        {
            Movimientos.Add(movimiento);
        }

        Paginador.Actualizar(resultado);
    }, "No se pudo consultar el kardex.");

    [RelayCommand]
    private Task ExportarExcelAsync() => ExportarAsync(FormatoExportacion.Excel);

    [RelayCommand]
    private Task ExportarPdfAsync() => ExportarAsync(FormatoExportacion.Pdf);

    [RelayCommand]
    private Task ExportarCsvAsync() => ExportarAsync(FormatoExportacion.Csv);

    private Task ExportarAsync(FormatoExportacion formato) => EjecutarAsync(async () =>
    {
        // El kardex se exporta a través del motor de reportes para que el archivo
        // salga con el mismo formato corporativo que el resto de informes.
        var reporte = await _reportes.GenerarAsync(new ParametrosReporte
        {
            Tipo = TipoReporte.Kardex,
            Desde = Desde ?? DateTime.Today.AddDays(-30),
            Hasta = Hasta ?? DateTime.Today
        }).ConfigureAwait(true);

        var nombre = _exportacion.SugerirNombreArchivo(reporte, formato);
        var extension = _exportacion.ObtenerExtension(formato);

        var ruta = _archivos.SeleccionarDondeGuardar(
            "Exportar kardex",
            $"Archivo {extension.TrimStart('.').ToUpperInvariant()}|*{extension}",
            nombre);

        if (string.IsNullOrWhiteSpace(ruta))
        {
            return;
        }

        await _exportacion.ExportarAsync(reporte, formato, ruta).ConfigureAwait(true);

        _dialogos.Notificar("Kardex exportado correctamente.");
        _archivos.AbrirConAplicacionPredeterminada(ruta);
    }, "No se pudo exportar el kardex.");

    [RelayCommand]
    private void LimpiarFiltros()
    {
        TextoBusqueda = null;
        TipoSeleccionado = null;
        Desde = DateTime.Today.AddDays(-30);
        Hasta = DateTime.Today;
    }
}
