using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Papeleria.App.Impresion;
using Papeleria.Business.DependencyInjection;
using Papeleria.Business.Security;
using Papeleria.Business.Services;
using Papeleria.Data;
using Papeleria.Data.Seed;
using Papeleria.Domain.Constants;

namespace Papeleria.Pruebas;

/// <summary>
/// Comprobaciones sobre una base de datos de usar y tirar, en una carpeta temporal.
/// La base real del usuario, en %LOCALAPPDATA%\PapeleriaApp, no se abre nunca.
///
///     dotnet run --project tests\Papeleria.Pruebas
///
/// Devuelve 0 si todo pasa y 1 si algo falla, para poder encadenarlo.
/// </summary>
public static class Programa
{
    private static int _correctas;
    private static int _fallos;

    [STAThread]
    public static int Main()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "papelsoft_pruebas_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);

        try
        {
            return Ejecutar(Path.Combine(carpeta, "prueba.db")).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FALLO GENERAL: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 2;
        }
        finally
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                Directory.Delete(carpeta, recursive: true);
            }
            catch
            {
                // Base desechable: si Windows la retiene, da igual.
            }
        }
    }

    private static async Task<int> Ejecutar(string archivo)
    {
        var proveedor = Construir(archivo);

        await proveedor.GetRequiredService<IInicializadorBaseDatos>()
            .InicializarAsync().ConfigureAwait(false);

        proveedor.GetRequiredService<IContextoSesion>().Iniciar(
            await proveedor.GetRequiredService<IServicioAutenticacion>()
                .AutenticarAsync(
                    SembradorDatos.UsuarioAdministrador,
                    SembradorDatos.ContrasenaAdministradorPorDefecto)
                .ConfigureAwait(false));

        await proveedor.GetRequiredService<IServicioConfiguracion>().CargarAsync().ConfigureAwait(false);

        await ComprobarCajaRegistradoraAsync(proveedor).ConfigureAwait(false);
        ComprobarQueLasPantallasCargan();

        Console.WriteLine();
        Console.WriteLine($"Comprobaciones correctas : {_correctas}");
        Console.WriteLine($"Fallos                   : {_fallos}");
        Console.WriteLine(_fallos == 0 ? "RESULTADO: OK" : "RESULTADO: HAY FALLOS");

        return _fallos == 0 ? 0 : 1;
    }

    private static IServiceProvider Construir(string archivo)
    {
        var servicios = new ServiceCollection();

        servicios.AddLogging();
        servicios.AgregarCapaNegocio();

        // La capa de datos apunta a %LOCALAPPDATA%; aquí se la desvía a un archivo
        // suelto para no tocar la base real.
        servicios.RemoveAll(typeof(IDbContextFactory<AppDbContext>));
        servicios.RemoveAll(typeof(DbContextOptions<AppDbContext>));

        servicios.AddPooledDbContextFactory<AppDbContext>(opciones =>
            opciones.UseSqlite($"Data Source={archivo};Foreign Keys=True"));

        return servicios.BuildServiceProvider();
    }

    // ── La caja registradora ────────────────────────────────────────────────

    /// <summary>
    /// El cajón del dinero no se puede probar de verdad sin el cacharro conectado a una
    /// impresora. Lo que sí se comprueba es todo lo que lo rodea, que es donde estas
    /// cosas se rompen.
    ///
    /// Y sobre todo los cinco bytes de la orden. Si estuvieran mal, el cajón no abriría
    /// y no pasaría nada más: ni error, ni ruido, ni aviso en pantalla. No habría forma
    /// de saber por qué.
    /// </summary>
    private static async Task ComprobarCajaRegistradoraAsync(IServiceProvider proveedor)
    {
        Console.WriteLine("Caja registradora");
        Console.WriteLine("─────────────────");

        var configuracion = proveedor.GetRequiredService<IServicioConfiguracion>();

        var impresion = new ServicioImpresion(configuracion, NullLogger<ServicioImpresion>.Instance);

        var cajon = new ServicioCajonMonedero(
            configuracion, impresion, NullLogger<ServicioCajonMonedero>.Instance);

        // ── Los cinco bytes ─────────────────────────────────────────────────
        var orden = typeof(ServicioCajonMonedero).GetMethod(
            "OrdenDeApertura", BindingFlags.NonPublic | BindingFlags.Static);

        Verificar("el servicio sabe formar la orden de apertura", orden is not null);

        if (orden is not null)
        {
            string Bytes(int patilla) =>
                Convert.ToHexString((byte[])orden.Invoke(null, new object[] { patilla })!);

            // ESC p 0 25 250: patilla 2, 50 ms de golpe y 500 de descanso.
            Verificar("la patilla 2 manda 1B700019FA", Bytes(2) == "1B700019FA", Bytes(2));

            // La misma orden con el tercer byte en 1 es la patilla 5.
            Verificar("la patilla 5 manda 1B700119FA", Bytes(5) == "1B700119FA", Bytes(5));

            Verificar("cualquier otro valor cae en la 2, que es la común",
                Bytes(7) == "1B700019FA", Bytes(7));
        }

        // ── Sin caja registradora ───────────────────────────────────────────
        Verificar("de fábrica no hay caja registradora", !cajon.EstaConectado);
        Verificar("y por tanto no se abre al cobrar", !cajon.AbreAlCobrar);

        // Lo más importante de todo: sin cajón, cobrar no puede tropezar con nada.
        Verificar("sin cajón, intentar abrirlo no molesta a la venta",
            cajon.IntentarAbrir() is null);

        Verificar("pedirlo a mano sin tenerlo configurado sí avisa",
            Rechaza(() => cajon.Abrir()));

        // ── Configurado a medias ────────────────────────────────────────────
        await configuracion.GuardarVariosAsync(new Dictionary<string, string?>
        {
            [ClavesConfiguracion.CajonModo] = "PorLaImpresora",
            [ClavesConfiguracion.ImpresoraRecibos] = string.Empty
        }).ConfigureAwait(false);

        Verificar("con el modo puesto, queda dado por conectado", cajon.EstaConectado);
        Verificar("y el abrir al cobrar viene encendido", cajon.AbreAlCobrar);

        var motivo = cajon.IntentarAbrir();

        Verificar("sin impresora de recibos avisa en vez de reventar",
            motivo is not null && motivo.Contains("impresora de recibos"), motivo);

        // ── La patilla ──────────────────────────────────────────────────────
        await configuracion.GuardarVariosAsync(new Dictionary<string, string?>
        {
            [ClavesConfiguracion.CajonPatilla] = "5"
        }).ConfigureAwait(false);

        var patilla = typeof(ServicioCajonMonedero)
            .GetProperty("Patilla", BindingFlags.NonPublic | BindingFlags.Instance);

        Verificar("la patilla elegida se respeta",
            patilla is not null && (int)patilla.GetValue(cajon)! == 5);

        // ── Y se puede quitar ───────────────────────────────────────────────
        await configuracion.GuardarVariosAsync(new Dictionary<string, string?>
        {
            [ClavesConfiguracion.CajonModo] = "Ninguno"
        }).ConfigureAwait(false);

        Verificar("quitarla lo deja todo como estaba",
            !cajon.EstaConectado && cajon.IntentarAbrir() is null);

        Verificar("preguntar por los puertos COM nunca falla",
            cajon.PuertosDisponibles() is not null);
    }

    // ── Que el XAML no esté roto ────────────────────────────────────────────

    /// <summary>
    /// Construye las pantallas de verdad. Un XAML mal cerrado o un recurso que no
    /// existe no lo ve el compilador de C#: revienta al abrir la pantalla, delante del
    /// cliente. Aquí revienta antes, que es donde debe.
    /// </summary>
    private static void ComprobarQueLasPantallasCargan()
    {
        Console.WriteLine();
        Console.WriteLine("Pantallas");
        Console.WriteLine("─────────");

        var app = new Application();

        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign2.Defaults.xaml",
                UriKind.Absolute)
        });

        foreach (var nombre in new[] { "Convertidores", "Estilos", "Controles", "PlantillasDatos" })
        {
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Papeleria;component/Resources/{nombre}.xaml",
                    UriKind.Absolute)
            });
        }

        var pantallas = new (string Nombre, Func<object> Construir)[]
        {
            ("Configuración", () => new App.Views.Paginas.ConfiguracionView()),
            ("Punto de venta", () => new App.Views.Paginas.PuntoVentaView()),
            ("Caja", () => new App.Views.Paginas.CajaView()),
            ("Acceso", () => new App.Views.LoginWindow())
        };

        foreach (var (nombre, construir) in pantallas)
        {
            try
            {
                construir();
                Verificar($"la pantalla de {nombre} carga", true);
            }
            catch (Exception ex)
            {
                Verificar($"la pantalla de {nombre} carga", false, $"{ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    // ── Andamiaje ───────────────────────────────────────────────────────────

    private static bool Rechaza(Action accion)
    {
        try
        {
            accion();
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static void Verificar(string titulo, bool condicion, string? detalle = null)
    {
        if (condicion)
        {
            _correctas++;
            Console.WriteLine($"  ok  {titulo}");
            return;
        }

        _fallos++;
        Console.WriteLine($"  X   {titulo}");

        if (!string.IsNullOrWhiteSpace(detalle))
        {
            Console.WriteLine($"        «{detalle}»");
        }
    }
}
