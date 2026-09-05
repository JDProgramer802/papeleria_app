using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Papeleria.App.Infrastructure;
using Papeleria.Business.Dtos;
using Papeleria.Business.Services;

namespace Papeleria.App.ViewModels.Dialogos;

/// <summary>
/// Carga del catálogo desde una hoja de Excel.
///
/// El orden importa: primero se lee y se enseña lo que se va a hacer con cada fila, y
/// solo después se toca la base de datos. Una importación que escribe primero y avisa
/// después deja al negocio con mil productos mal creados y sin forma cómoda de deshacer.
/// </summary>
public partial class ImportacionDialogoVistaModelo : VistaModeloBase
{
    private readonly IServicioImportacion _importacion;
    private readonly IServicioArchivos _archivos;
    private readonly IServicioDialogos _dialogos;

    public ImportacionDialogoVistaModelo(
        IServicioImportacion importacion, IServicioArchivos archivos, IServicioDialogos dialogos)
    {
        _importacion = importacion;
        _archivos = archivos;
        _dialogos = dialogos;

        Titulo = "Importar productos desde Excel";
    }

    public ObservableCollection<FilaImportacion> Filas { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayArchivo))]
    private string _archivo = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayPrevisualizacion))]
    [NotifyPropertyChangedFor(nameof(SePuedeImportar))]
    [NotifyPropertyChangedFor(nameof(Resumen))]
    [NotifyCanExecuteChangedFor(nameof(ImportarCommand))]
    private PrevisualizacionImportacion? _previsualizacion;

    [ObservableProperty] private bool _ocupado;

    public bool HayArchivo => !string.IsNullOrWhiteSpace(Archivo);

    public bool HayPrevisualizacion => Previsualizacion is not null;

    public bool SePuedeImportar => Previsualizacion?.SePuedeImportar == true;

    public string Resumen => Previsualizacion?.Resumen ?? string.Empty;

    public bool HayDescartadas => Previsualizacion?.Descartados > 0;

    [RelayCommand]
    private Task ElegirArchivoAsync() => Ejecutar(async () =>
    {
        var ruta = _archivos.SeleccionarArchivo(
            "Hoja de productos", "Hojas de Excel (*.xlsx)|*.xlsx|Todos los archivos|*.*");

        if (string.IsNullOrWhiteSpace(ruta))
        {
            return;
        }

        Archivo = ruta;
        Previsualizacion = await _importacion.PrevisualizarAsync(ruta).ConfigureAwait(true);

        Filas.Clear();

        foreach (var fila in Previsualizacion.Filas)
        {
            Filas.Add(fila);
        }

        OnPropertyChanged(nameof(HayDescartadas));
    });

    /// <summary>
    /// Deja una plantilla con los encabezados y tres ejemplos. Sin ella, la primera
    /// carga falla siempre porque nadie adivina cómo hay que llamar las columnas.
    /// </summary>
    [RelayCommand]
    private Task DescargarPlantillaAsync() => Ejecutar(async () =>
    {
        var carpeta = _archivos.SeleccionarCarpeta("¿Dónde guardo la plantilla?");

        if (string.IsNullOrWhiteSpace(carpeta))
        {
            return;
        }

        var ruta = await _importacion
            .GenerarPlantillaAsync(System.IO.Path.Combine(carpeta, "plantilla-productos.xlsx"))
            .ConfigureAwait(true);

        _archivos.AbrirConAplicacionPredeterminada(ruta);
    });

    private bool PuedeImportar() => SePuedeImportar && !Ocupado;

    [RelayCommand(CanExecute = nameof(PuedeImportar))]
    private async Task ImportarAsync()
    {
        if (Previsualizacion is null)
        {
            return;
        }

        var confirmado = await _dialogos.ConfirmarAsync(
            "Importar productos",
            $"{Previsualizacion.Resumen}.\n\n" +
            "Los que ya existen conservan sus existencias: el inventario se mueve por el " +
            "kardex, no por una hoja de cálculo. ¿Continuar?",
            "Importar").ConfigureAwait(true);

        if (!confirmado)
        {
            return;
        }

        await Ejecutar(async () =>
        {
            var resultado = await _importacion.ImportarAsync(Previsualizacion).ConfigureAwait(true);

            await _dialogos.InformarAsync("Importación terminada", resultado.Resumen)
                .ConfigureAwait(true);

            _dialogos.Cerrar(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private void Cancelar() => _dialogos.Cerrar(false);

    private async Task Ejecutar(Func<Task> accion)
    {
        Ocupado = true;
        MensajeError = null;
        ImportarCommand.NotifyCanExecuteChanged();

        try
        {
            await accion().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MensajeError = ex.Message;
        }
        finally
        {
            Ocupado = false;
            ImportarCommand.NotifyCanExecuteChanged();
        }
    }
}
