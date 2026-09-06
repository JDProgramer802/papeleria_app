using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Papeleria.App.Infrastructure;
using Papeleria.Business.Common;
using Papeleria.Business.Services;

namespace Papeleria.App.ViewModels.Dialogos;

/// <summary>
/// Aviso de versión nueva. Descarga el ejecutable mostrando el avance y, al terminar,
/// ofrece reiniciar para trabajar ya con la versión actualizada.
/// </summary>
public partial class ActualizacionDialogoVistaModelo : VistaModeloBase
{
    private readonly IServicioActualizaciones _actualizaciones;
    private readonly IServicioDialogos _dialogos;
    private CancellationTokenSource? _cancelacion;

    public ActualizacionDialogoVistaModelo(
        IServicioActualizaciones actualizaciones,
        IServicioDialogos dialogos,
        ActualizacionDisponible actualizacion)
    {
        _actualizaciones = actualizaciones;
        _dialogos = dialogos;

        Actualizacion = actualizacion;
        Titulo = "Hay una versión nueva disponible";
        VersionActual = actualizaciones.VersionActual.ToString(3);
        VersionNueva = actualizacion.Version.ToString(3);
    }

    public ActualizacionDisponible Actualizacion { get; }

    public string VersionActual { get; }

    public string VersionNueva { get; }

    public bool TieneNotas => !string.IsNullOrWhiteSpace(Actualizacion.Notas);

    public string FechaPublicacion => Actualizacion.Publicada is { } fecha
        ? $"Publicada el {Formatos.Fecha(fecha)}"
        : string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeCerrar))]
    private bool _descargando;

    [ObservableProperty]
    private double _progreso;

    [ObservableProperty]
    private string _textoProgreso = string.Empty;

    /// <summary>Tras sustituir el ejecutable solo queda reiniciar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeCerrar))]
    private bool _listaParaReiniciar;

    public bool PuedeCerrar => !Descargando;

    [RelayCommand]
    private async Task ActualizarAhoraAsync()
    {
        _cancelacion = new CancellationTokenSource();

        Descargando = true;
        MensajeError = null;
        Progreso = 0;
        TextoProgreso = $"Descargando {Actualizacion.TamanoTexto}…";

        try
        {
            var progreso = new Progress<double>(porcentaje =>
            {
                Progreso = porcentaje;
                TextoProgreso = $"Descargando… {porcentaje:N0} % de {Actualizacion.TamanoTexto}";
            });

            var archivo = await _actualizaciones
                .DescargarAsync(Actualizacion, progreso, _cancelacion.Token)
                .ConfigureAwait(true);

            TextoProgreso = "Instalando la actualización…";

            await _actualizaciones.AplicarAsync(archivo, _cancelacion.Token).ConfigureAwait(true);

            ListaParaReiniciar = true;
            TextoProgreso = "Actualización lista.";
        }
        catch (OperationCanceledException)
        {
            TextoProgreso = string.Empty;
        }
        catch (Domain.Exceptions.NegocioException ex)
        {
            MensajeError = ex.Message;
            TextoProgreso = string.Empty;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Fallo al actualizar la aplicación");
            MensajeError = "No se pudo completar la actualización. Revise el registro de errores.";
            TextoProgreso = string.Empty;
        }
        finally
        {
            Descargando = false;
        }
    }

    [RelayCommand]
    private void Reiniciar()
    {
        var ejecutable = Environment.ProcessPath;

        if (!string.IsNullOrWhiteSpace(ejecutable))
        {
            // El argumento le dice a la copia nueva que espere a que esta termine de
            // cerrarse —respaldo de cierre incluido— en vez de chocar con el semáforo de
            // instancia única y morir con «El sistema ya está abierto».
            Process.Start(new ProcessStartInfo(ejecutable)
            {
                UseShellExecute = true,
                Arguments = App.ArgumentoTrasActualizar
            });
        }

        System.Windows.Application.Current.Shutdown();
    }

    [RelayCommand]
    private void Cancelar()
    {
        if (Descargando)
        {
            _cancelacion?.Cancel();
            return;
        }

        _dialogos.Cerrar(false);
    }

    [RelayCommand]
    private async Task OmitirVersionAsync()
    {
        await _actualizaciones.OmitirVersionAsync(Actualizacion.Version).ConfigureAwait(true);
        _dialogos.Cerrar(false);
    }
}
