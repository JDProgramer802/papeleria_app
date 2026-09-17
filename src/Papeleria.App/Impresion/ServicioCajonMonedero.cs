using System.IO.Ports;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Papeleria.Business.Services;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Exceptions;

namespace Papeleria.App.Impresion;

/// <summary>Por dónde se le habla al cajón.</summary>
public enum ModoCajon
{
    /// <summary>No hay cajón conectado; el negocio guarda el dinero a mano.</summary>
    Ninguno = 0,

    /// <summary>Colgado de la impresora de recibos, que es como viene el 95 % de ellos.</summary>
    PorLaImpresora = 1,

    /// <summary>Colgado de otra impresora distinta a la de los recibos.</summary>
    PorOtraImpresora = 2,

    /// <summary>Conectado directamente a un puerto COM del computador.</summary>
    PorPuertoSerie = 3
}

/// <summary>
/// Abre el cajón del dinero de la caja registradora.
///
/// Conviene saber cómo funciona esto de verdad, porque no es evidente: el cajón no se
/// conecta al computador. Se conecta a la impresora de recibos con un cable de teléfono
/// (RJ11/RJ12), y la impresora lleva dentro un relé que le da un golpe de corriente a la
/// cerradura. Lo único que hace el programa es mandarle a la impresora una orden de tres
/// bytes —la de toda la vida en el juego de comandos ESC/POS— y la impresora se encarga
/// del resto.
///
/// De ahí salen las dos cosas que más confunden al instalarlo:
///
///   1. Hay que mandar la orden EN CRUDO a la cola de impresión. Lo que usa el resto del
///      programa para imprimir la tirilla, PrintVisual, dibuja una imagen: si por ahí se
///      mandaran estos bytes, la impresora sacaría un papel con símbolos raros y el
///      cajón ni se enteraría.
///   2. El cajón cuelga de una de dos patillas del conector, la 2 o la 5, y eso lo
///      decide el fabricante del cajón, no el de la impresora. Si se manda a la patilla
///      equivocada no pasa absolutamente nada: ni ruido, ni error, ni aviso. Por eso la
///      configuración deja elegirla y hay un botón de probar: es la única forma de
///      acertar sin abrir el cable.
/// </summary>
public interface IServicioCajonMonedero
{
    ModoCajon Modo { get; }

    /// <summary>Hay un cajón configurado al que se le puede hablar.</summary>
    bool EstaConectado { get; }

    /// <summary>Se abre solo al terminar una venta cobrada en efectivo.</summary>
    bool AbreAlCobrar { get; }

    /// <summary>Puertos serie que el equipo tiene ahora mismo.</summary>
    IReadOnlyList<string> PuertosDisponibles();

    /// <summary>Le da el golpe al cajón. Lanza si no se pudo.</summary>
    void Abrir();

    /// <summary>
    /// Abre el cajón sin interrumpir lo que se esté haciendo. Devuelve el motivo si no
    /// se pudo, o <c>null</c> si abrió. La venta ya está cobrada cuando se llama a esto:
    /// un cajón que no responde no puede tumbar una venta que ya se guardó.
    /// </summary>
    string? IntentarAbrir();
}

/// <inheritdoc cref="IServicioCajonMonedero" />
public class ServicioCajonMonedero : IServicioCajonMonedero
{
    private readonly IServicioConfiguracion _configuracion;
    private readonly IServicioImpresion _impresion;
    private readonly ILogger<ServicioCajonMonedero> _log;

    public ServicioCajonMonedero(
        IServicioConfiguracion configuracion,
        IServicioImpresion impresion,
        ILogger<ServicioCajonMonedero> log)
    {
        _configuracion = configuracion;
        _impresion = impresion;
        _log = log;
    }

    public ModoCajon Modo =>
        Enum.TryParse<ModoCajon>(_configuracion.ObtenerTexto(ClavesConfiguracion.CajonModo), out var modo)
            ? modo
            : ModoCajon.Ninguno;

    public bool EstaConectado => Modo != ModoCajon.Ninguno;

    public bool AbreAlCobrar =>
        EstaConectado && _configuracion.ObtenerBooleano(ClavesConfiguracion.CajonAbrirAlCobrar, true);

    /// <summary>Patilla del conector a la que responde el cajón: la 2 o la 5.</summary>
    private int Patilla => _configuracion.ObtenerEntero(ClavesConfiguracion.CajonPatilla, 2) == 5 ? 5 : 2;

    public IReadOnlyList<string> PuertosDisponibles()
    {
        try
        {
            return SerialPort.GetPortNames().OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudieron consultar los puertos serie");
            return Array.Empty<string>();
        }
    }

    public string? IntentarAbrir()
    {
        if (!EstaConectado)
        {
            return null;
        }

        try
        {
            Abrir();
            return null;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudo abrir el cajón del dinero");
            return ex.Message;
        }
    }

    public void Abrir()
    {
        var orden = OrdenDeApertura(Patilla);

        switch (Modo)
        {
            case ModoCajon.Ninguno:
                throw new NegocioException(
                    "No hay ninguna caja registradora configurada. " +
                    "Configúrela en Configuración → Caja registradora.");

            case ModoCajon.PorPuertoSerie:
                AbrirPorPuertoSerie(orden);
                break;

            default:
                AbrirPorImpresora(orden);
                break;
        }

        _log.LogInformation("Cajón abierto por {Modo}, patilla {Patilla}", Modo, Patilla);
    }

    /// <summary>
    /// La orden ESC/POS de toda la vida: ESC p, la patilla, y cuánto dura el golpe.
    ///
    /// Los dos últimos bytes se cuentan en unidades de 2 ms: 25 son 50 ms de corriente,
    /// que es lo que tarda el solenoide en soltar el pestillo, y 250 son 500 ms de
    /// descanso para que no se pida otro golpe encima del anterior. Son los valores que
    /// traen de fábrica prácticamente todas las térmicas de 80 mm.
    /// </summary>
    private static byte[] OrdenDeApertura(int patilla) =>
        new byte[] { 0x1B, 0x70, (byte)(patilla == 5 ? 1 : 0), 25, 250 };

    private void AbrirPorImpresora(byte[] orden)
    {
        var impresora = Modo == ModoCajon.PorOtraImpresora
            ? _configuracion.ObtenerTexto(ClavesConfiguracion.CajonImpresora)
            : _impresion.ImpresoraDeRecibos;

        if (string.IsNullOrWhiteSpace(impresora))
        {
            throw new NegocioException(Modo == ModoCajon.PorOtraImpresora
                ? "No se ha elegido a qué impresora está conectado el cajón."
                : "El cajón está configurado para abrirse por la impresora de recibos, " +
                  "pero no hay ninguna impresora de recibos elegida.");
        }

        ColaDeImpresion.EnviarEnCrudo(impresora, orden, "Apertura de cajón");
    }

    private void AbrirPorPuertoSerie(byte[] orden)
    {
        var puerto = _configuracion.ObtenerTexto(ClavesConfiguracion.CajonPuerto);

        if (string.IsNullOrWhiteSpace(puerto))
        {
            throw new NegocioException("No se ha elegido el puerto COM del cajón.");
        }

        try
        {
            using var serie = new SerialPort(puerto, 9600, Parity.None, 8, StopBits.One)
            {
                WriteTimeout = 2000,
                Handshake = Handshake.None,
                DtrEnable = true,
                RtsEnable = true
            };

            serie.Open();
            serie.Write(orden, 0, orden.Length);
            serie.BaseStream.Flush();
        }
        catch (Exception ex)
        {
            throw new NegocioException(
                $"No se pudo hablar con el cajón por el puerto {puerto}. {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Manda bytes tal cual a una impresora, sin que Windows los interprete.
///
/// La cola de impresión normalmente traduce lo que se le da al idioma de la impresora.
/// Para una orden de control como la del cajón hace falta lo contrario: que no toque
/// nada. Eso es lo que significa el tipo de datos «RAW» de la API del spooler, y es la
/// única vía desde .NET sin abrir el puerto de la impresora a mano.
/// </summary>
internal static class ColaDeImpresion
{
    public static void EnviarEnCrudo(string impresora, byte[] datos, string titulo)
    {
        if (!OpenPrinter(impresora, out var manija, IntPtr.Zero))
        {
            throw new NegocioException(
                $"No se pudo abrir la impresora «{impresora}» para hablarle al cajón. " +
                Descripcion(Marshal.GetLastWin32Error()));
        }

        var bufer = IntPtr.Zero;

        try
        {
            var documento = new DocInfo
            {
                NombreDocumento = titulo,
                ArchivoSalida = null,
                TipoDatos = "RAW"
            };

            if (!StartDocPrinter(manija, 1, ref documento))
            {
                throw new NegocioException(
                    $"La impresora «{impresora}» no aceptó la orden del cajón. " +
                    Descripcion(Marshal.GetLastWin32Error()));
            }

            try
            {
                if (!StartPagePrinter(manija))
                {
                    throw new NegocioException(
                        $"La impresora «{impresora}» no aceptó la orden del cajón. " +
                        Descripcion(Marshal.GetLastWin32Error()));
                }

                bufer = Marshal.AllocCoTaskMem(datos.Length);
                Marshal.Copy(datos, 0, bufer, datos.Length);

                if (!WritePrinter(manija, bufer, datos.Length, out var escritos) ||
                    escritos != datos.Length)
                {
                    throw new NegocioException(
                        $"La orden no llegó completa a «{impresora}». " +
                        Descripcion(Marshal.GetLastWin32Error()));
                }

                EndPagePrinter(manija);
            }
            finally
            {
                EndDocPrinter(manija);
            }
        }
        finally
        {
            if (bufer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(bufer);
            }

            ClosePrinter(manija);
        }
    }

    private static string Descripcion(int codigo) =>
        codigo == 0 ? string.Empty : $"(error {codigo} de Windows: {new System.ComponentModel.Win32Exception(codigo).Message})";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string NombreDocumento;
        [MarshalAs(UnmanagedType.LPWStr)] public string? ArchivoSalida;
        [MarshalAs(UnmanagedType.LPWStr)] public string TipoDatos;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool OpenPrinter(string nombre, out IntPtr manija, IntPtr valoresPorDefecto);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr manija);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool StartDocPrinter(IntPtr manija, int nivel, ref DocInfo documento);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr manija);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr manija);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr manija);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr manija, IntPtr datos, int cuantos, out int escritos);
}
