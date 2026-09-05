using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Papeleria.Business.Dtos;
using Papeleria.Business.Services;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Exceptions;

namespace Papeleria.App.Impresion;

/// <summary>Impresora instalada en el equipo, tal como se ofrece en Configuración.</summary>
public record ImpresoraDisponible(string Nombre, bool EsPredeterminada)
{
    public string Descripcion => EsPredeterminada ? $"{Nombre}  (predeterminada)" : Nombre;
}

public interface IServicioImpresion
{
    /// <summary>Impresoras instaladas; la lista se pide al sistema en cada consulta.</summary>
    IReadOnlyList<ImpresoraDisponible> Listar();

    /// <summary>Impresora configurada para las tirillas; vacío si no se ha elegido ninguna.</summary>
    string ImpresoraDeRecibos { get; }

    /// <summary>El recibo sale solo al cobrar, sin pasar por el visor de PDF.</summary>
    bool ImprimeAutomatico { get; }

    /// <summary>Manda la tirilla a la impresora configurada.</summary>
    void ImprimirRecibo(VentaDetalladaDto venta);

    /// <summary>Tirilla de prueba, para comprobar que la impresora responde.</summary>
    void ImprimirPrueba();
}

/// <inheritdoc cref="IServicioImpresion" />
public class ServicioImpresion : IServicioImpresion
{
    /// <summary>
    /// Ancho del papel en unidades de WPF (1/96 de pulgada). 80 mm de papel dejan unos
    /// 72 mm imprimibles: el resto se lo comen los márgenes del cabezal.
    /// </summary>
    private const double AnchoTirilla = 72 / 25.4 * 96;

    private readonly IServicioConfiguracion _configuracion;
    private readonly ILogger<ServicioImpresion> _log;

    public ServicioImpresion(IServicioConfiguracion configuracion, ILogger<ServicioImpresion> log)
    {
        _configuracion = configuracion;
        _log = log;
    }

    public string ImpresoraDeRecibos =>
        _configuracion.ObtenerTexto(ClavesConfiguracion.ImpresoraRecibos);

    public bool ImprimeAutomatico =>
        _configuracion.ObtenerBooleano(ClavesConfiguracion.ImprimirReciboAutomatico);

    public IReadOnlyList<ImpresoraDisponible> Listar()
    {
        try
        {
            using var servidor = new LocalPrintServer();
            var predeterminada = servidor.DefaultPrintQueue?.FullName;

            return servidor
                .GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections })
                .Select(c => new ImpresoraDisponible(
                    c.FullName,
                    string.Equals(c.FullName, predeterminada, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(i => i.EsPredeterminada)
                .ThenBy(i => i.Nombre)
                .ToList();
        }
        catch (Exception ex)
        {
            // Un equipo sin cola de impresión no debe impedir abrir la configuración.
            _log.LogWarning(ex, "No se pudo consultar la lista de impresoras");
            return Array.Empty<ImpresoraDisponible>();
        }
    }

    public void ImprimirRecibo(VentaDetalladaDto venta)
    {
        var ticket = new ReciboTicket
        {
            DataContext = new ReciboTicketDatos
            {
                Venta = venta,
                Empresa = _configuracion.ObtenerEmpresa()
            }
        };

        Imprimir(ticket, $"Recibo {venta.NumeroFactura}");
    }

    public void ImprimirPrueba()
    {
        var aviso = new TextBlock
        {
            Text = "PapelSoft\n\nImpresión de prueba\n\nSi lee esto, la impresora quedó bien configurada.\n\n" +
                   DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10, 14, 10, 20),
            Background = Brushes.White,
            Foreground = Brushes.Black
        };

        Imprimir(aviso, "Prueba de impresión");
    }

    /// <summary>
    /// Dibuja el control al ancho del papel y lo manda a la cola.
    ///
    /// Se usa <c>PrintVisual</c> y no un documento paginado porque la tirilla es una
    /// sola tira continua: en una térmica el papel no tiene alto, se corta donde
    /// termina lo impreso.
    /// </summary>
    private void Imprimir(FrameworkElement contenido, string titulo)
    {
        var nombre = ImpresoraDeRecibos;

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new NegocioException(
                "No hay ninguna impresora elegida para los recibos. " +
                "Configúrela en Configuración → Impresión.");
        }

        PrintQueue cola;

        try
        {
            using var servidor = new LocalPrintServer();
            cola = servidor.GetPrintQueue(nombre);
        }
        catch (Exception ex)
        {
            throw new NegocioException(
                $"No se encontró la impresora «{nombre}». Puede que se haya " +
                "desconectado o cambiado de nombre.", ex);
        }

        var dialogo = new PrintDialog { PrintQueue = cola };

        // El alto se deja crecer con el contenido; el ancho manda.
        contenido.Width = AnchoTirilla;
        contenido.Measure(new Size(AnchoTirilla, double.PositiveInfinity));
        contenido.Arrange(new Rect(new Point(0, 0), contenido.DesiredSize));
        contenido.UpdateLayout();

        try
        {
            dialogo.PrintVisual(contenido, titulo);
        }
        catch (Exception ex)
        {
            throw new NegocioException(
                $"No se pudo imprimir en «{nombre}». {ex.Message}", ex);
        }

        _log.LogInformation("Impreso «{Titulo}» en {Impresora}", titulo, nombre);
    }
}
