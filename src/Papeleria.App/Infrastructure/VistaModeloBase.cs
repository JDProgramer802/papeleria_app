using CommunityToolkit.Mvvm.ComponentModel;

namespace Papeleria.App.Infrastructure;

/// <summary>
/// Base de todos los modelos de vista. Aporta el indicador de carga y el mensaje de
/// error que las vistas usan para mostrar el estado de una operación asíncrona.
/// </summary>
public abstract partial class VistaModeloBase : ObservableObject
{
    [ObservableProperty]
    private bool _estaCargando;

    [ObservableProperty]
    private string? _mensajeError;

    [ObservableProperty]
    private string _titulo = string.Empty;

    /// <summary>Descripción corta mostrada bajo el título de la página.</summary>
    [ObservableProperty]
    private string _subtitulo = string.Empty;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    partial void OnMensajeErrorChanged(string? value) => OnPropertyChanged(nameof(TieneError));

    /// <summary>
    /// Se invoca cada vez que la página pasa a estar visible. Las implementaciones
    /// cargan aquí sus datos para no bloquear la navegación.
    /// </summary>
    public virtual Task CargarAsync() => Task.CompletedTask;

    /// <summary>Ejecuta una operación mostrando el indicador de carga y capturando errores.</summary>
    protected async Task EjecutarAsync(Func<Task> operacion, string? mensajeErrorGenerico = null)
    {
        if (EstaCargando)
        {
            return;
        }

        EstaCargando = true;
        MensajeError = null;

        try
        {
            await operacion().ConfigureAwait(true);
        }
        catch (Domain.Exceptions.NegocioException ex)
        {
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error no controlado en {VistaModelo}", GetType().Name);
            MensajeError = mensajeErrorGenerico ?? "Ocurrió un error inesperado. Revise el registro de errores.";
        }
        finally
        {
            EstaCargando = false;
        }
    }
}

/// <summary>Modelos de vista que representan una página del menú lateral.</summary>
public abstract class PaginaVistaModelo : VistaModeloBase
{
    /// <summary>Clave del módulo, usada para resolver permisos y navegación.</summary>
    public abstract string Modulo { get; }
}
