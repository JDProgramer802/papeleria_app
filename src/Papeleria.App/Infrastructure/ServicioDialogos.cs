using System.Windows;
using MaterialDesignThemes.Wpf;
using Papeleria.App.ViewModels.Dialogos;

namespace Papeleria.App.Infrastructure;

/// <inheritdoc cref="IServicioDialogos" />
public class ServicioDialogos : IServicioDialogos
{
    /// <summary>Identificador del <c>DialogHost</c> declarado en la ventana principal.</summary>
    public const string HostRaiz = "HostDialogos";

    private readonly ISnackbarMessageQueue _colaMensajes;

    public ServicioDialogos(ISnackbarMessageQueue colaMensajes) => _colaMensajes = colaMensajes;

    public async Task<bool> ConfirmarAsync(
        string titulo,
        string mensaje,
        string textoAceptar = "Aceptar",
        string textoCancelar = "Cancelar",
        bool esDestructivo = false)
    {
        var modelo = new DialogoMensajeVistaModelo
        {
            Titulo = titulo,
            Mensaje = mensaje,
            TextoAceptar = textoAceptar,
            TextoCancelar = textoCancelar,
            MostrarCancelar = true,
            EsDestructivo = esDestructivo,
            Icono = esDestructivo ? PackIconKind.AlertCircleOutline : PackIconKind.HelpCircleOutline
        };

        return await MostrarAsync(modelo).ConfigureAwait(true) is true;
    }

    public async Task InformarAsync(string titulo, string mensaje, bool esError = false)
    {
        var modelo = new DialogoMensajeVistaModelo
        {
            Titulo = titulo,
            Mensaje = mensaje,
            TextoAceptar = "Entendido",
            MostrarCancelar = false,
            EsDestructivo = esError,
            Icono = esError ? PackIconKind.AlertCircleOutline : PackIconKind.InformationOutline
        };

        await MostrarAsync(modelo).ConfigureAwait(true);
    }

    public async Task<string?> PedirTextoAsync(
        string titulo,
        string etiqueta,
        string? valorInicial = null,
        bool multilinea = false,
        bool obligatorio = true)
    {
        var modelo = new DialogoTextoVistaModelo
        {
            Titulo = titulo,
            Etiqueta = etiqueta,
            Valor = valorInicial ?? string.Empty,
            Multilinea = multilinea,
            Obligatorio = obligatorio
        };

        return await MostrarAsync(modelo).ConfigureAwait(true) as string;
    }

    public async Task<object?> MostrarAsync(object modeloVista)
    {
        // Los diálogos deben abrirse en el hilo de interfaz aunque se soliciten
        // desde la continuación de una tarea en segundo plano.
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            return await Application.Current.Dispatcher
                .InvokeAsync(async () => await MostrarAsync(modeloVista).ConfigureAwait(true))
                .Task.Unwrap().ConfigureAwait(true);
        }

        try
        {
            return await DialogHost.Show(modeloVista, HostRaiz).ConfigureAwait(true);
        }
        catch (InvalidOperationException)
        {
            // Ocurre si el host todavía no está en el árbol visual (por ejemplo, durante
            // el arranque). Se degrada a un cuadro de diálogo estándar de Windows.
            return MostrarComoCuadroDeMensaje(modeloVista);
        }
    }

    private static object? MostrarComoCuadroDeMensaje(object modeloVista)
    {
        if (modeloVista is not DialogoMensajeVistaModelo mensaje)
        {
            return null;
        }

        var botones = mensaje.MostrarCancelar ? MessageBoxButton.OKCancel : MessageBoxButton.OK;
        var icono = mensaje.EsDestructivo ? MessageBoxImage.Warning : MessageBoxImage.Information;

        var resultado = MessageBox.Show(mensaje.Mensaje, mensaje.Titulo, botones, icono);

        return resultado == MessageBoxResult.OK;
    }

    public void Cerrar(object? resultado = null)
    {
        if (DialogHost.IsDialogOpen(HostRaiz))
        {
            DialogHost.Close(HostRaiz, resultado);
        }
    }

    public void Notificar(string mensaje)
    {
        if (!string.IsNullOrWhiteSpace(mensaje))
        {
            _colaMensajes.Enqueue(mensaje);
        }
    }
}
