using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Papeleria.App.Impresion;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;
using Papeleria.Business.DependencyInjection;
using Papeleria.Business.Security;
using Papeleria.Business.Services;
using Papeleria.Data;
using Papeleria.Data.Seed;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Enums;

namespace Papeleria.Pruebas;

/// <summary>
/// Comprobaciones sobre una base de datos de usar y tirar, en una carpeta temporal.
/// La base real del usuario, en %LOCALAPPDATA%\PapeleriaApp, no se abre nunca.
///
///     dotnet run --project tests\Papeleria.Pruebas
///
/// Devuelve 0 si todo pasa y 1 si algo falla, para poder encadenarlo.
/// </summary>
public static partial class Programa
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
        await ComprobarMigracionGastosAsync(archivo + ".upgrade");
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
        ComprobarGastosVisuales(proveedor);
        await ComprobarFacturaAsync(proveedor).ConfigureAwait(false);
        await ComprobarGastosAsync(proveedor).ConfigureAwait(false);

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

        var impresion = new ServicioImpresion(configuracion, proveedor.GetRequiredService<IServicioCodigoBarras>(), NullLogger<ServicioImpresion>.Instance);

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
            ("Gastos", () => new App.Views.Paginas.GastosView()),
            ("Inicio", () => new App.Views.Paginas.DashboardView()),
            ("Ventana principal", () => new App.Views.MainWindow()),
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

    // ── La factura ──────────────────────────────────────────────────────────

    /// <summary>
    /// Lo que va impreso en la factura. El valor en letras y el cuadro del IVA son lo
    /// que mira un contador; si uno de los dos no cuadra con el total, el soporte no
    /// sirve. Y la tirilla tiene que poder dibujarse: si su XAML se rompe, la venta
    /// queda hecha pero el cliente se va sin comprobante.
    ///
    /// Con la variable PAPELSOFT_MUESTRAS apuntando a una carpeta, deja ahí la tirilla
    /// en PNG y los PDF, para mirarlos.
    /// </summary>
    private static async Task ComprobarFacturaAsync(IServiceProvider proveedor)
    {
        Console.WriteLine();
        Console.WriteLine("Factura");
        Console.WriteLine("───────");

        foreach (var (valor, esperado) in new[]
                 {
                     (1m, "UN PESO M/CTE"),
                     (21_000m, "VEINTIÚN MIL PESOS M/CTE"),
                     (15_400m, "QUINCE MIL CUATROCIENTOS PESOS M/CTE"),
                     (100m, "CIEN PESOS M/CTE"),
                     (101m, "CIENTO UN PESOS M/CTE"),
                     (1_000_000m, "UN MILLÓN DE PESOS M/CTE"),
                     (2_531_731m, "DOS MILLONES QUINIENTOS TREINTA Y UN MIL SETECIENTOS TREINTA Y UN PESOS M/CTE"),
                     (1_000_000_000m, "MIL MILLONES DE PESOS M/CTE"),
                     (1_250.5m, "MIL DOSCIENTOS CINCUENTA PESOS CON 50/100 M/CTE")
                 })
        {
            var obtenido = NumeroEnLetras.Pesos(valor);
            Verificar($"{valor} se escribe «{esperado}»", obtenido == esperado, obtenido);
        }

        var configuracion = proveedor.GetRequiredService<IServicioConfiguracion>();

        await configuracion.GuardarVariosAsync(new Dictionary<string, string?>
        {
            [ClavesConfiguracion.EmpresaNombre] = "Papelería El Lápiz",
            [ClavesConfiguracion.EmpresaNit] = "900.123.456-7",
            [ClavesConfiguracion.EmpresaRegimen] = "Responsable de IVA",
            [ClavesConfiguracion.EmpresaDireccion] = "Cra. 7 # 12-34",
            [ClavesConfiguracion.EmpresaCiudad] = "Bogotá",
            [ClavesConfiguracion.EmpresaTelefono] = "601 555 1234",
            [ClavesConfiguracion.FacturaPrefijo] = "FV-",
            [ClavesConfiguracion.FacturaResolucionNumero] = "18764000001234",
            [ClavesConfiguracion.FacturaResolucionFecha] = "2026-01-15",
            [ClavesConfiguracion.FacturaResolucionDesde] = "1",
            [ClavesConfiguracion.FacturaResolucionHasta] = "5000",
            [ClavesConfiguracion.FacturaResolucionVigencia] = "2028-01-15"
        }).ConfigureAwait(false);

        var empresa = configuracion.ObtenerEmpresa();
        var venta = VentaDeMuestra();
        var recibo = new ReciboVenta(venta, empresa);

        Verificar("la resolución sale completa, con prefijo y vigencia",
            recibo.Resolucion.Count == 3
            && recibo.Resolucion[0] == "Resolución DIAN N.º 18764000001234 del 2026-01-15"
            && recibo.Resolucion[1] == "Prefijo FV del 1 al 5000"
            && recibo.Resolucion[2] == "Vigencia hasta 2028-01-15",
            string.Join(" | ", recibo.Resolucion));

        Verificar("el régimen va en el encabezado", recibo.Encabezado.Contains("Responsable de IVA"));
        Verificar("el título por defecto es «FACTURA DE VENTA»", recibo.Titulo == "FACTURA DE VENTA", recibo.Titulo);

        Verificar("hay una fila de IVA por tarifa, de mayor a menor",
            recibo.Impuestos.Select(i => i.Tarifa).SequenceEqual(new[] { "IVA 19 %", "IVA 5 %", "Excluido" }),
            string.Join(", ", recibo.Impuestos.Select(i => i.Tarifa)));

        Verificar("cada renglón lleva la letra de su tarifa",
            recibo.Lineas.Select(l => l.MarcaIva).SequenceEqual(new[] { "A", "C", "B" }),
            string.Join(", ", recibo.Lineas.Select(l => l.MarcaIva)));

        Verificar("la base del 19 % ya tiene restado el descuento",
            recibo.Impuestos[0].Base == Formatos.Moneda(9_000m), recibo.Impuestos[0].Base);

        Verificar("el total en letras es el del total",
            recibo.TotalEnLetras == $"SON: {NumeroEnLetras.Pesos(venta.Total)}", recibo.TotalEnLetras);

        var sinIva = new ReciboVenta(new VentaDetalladaDto
        {
            NumeroFactura = "FV-000002",
            Fecha = DateTime.Now,
            ClienteNombre = "Consumidor final",
            Subtotal = 5_000m,
            Total = 5_000m,
            Lineas =
            {
                new LineaDocumentoDto
                {
                    Descripcion = "Cuaderno", Cantidad = 1, ValorUnitario = 5_000m, Subtotal = 5_000m, Total = 5_000m
                }
            }
        }, empresa);

        Verificar("sin IVA no se imprime un cuadro de ceros",
            !sinIva.DiscriminaIva && sinIva.Impuestos.Count == 0 && sinIva.Lineas[0].MarcaIva == "");

        Verificar("la copia se marca como copia", new ReciboVenta(venta, empresa, esCopia: true).EsCopia);

        // ── Que se dibuje ───────────────────────────────────────────────────
        var impresion = new ServicioImpresion(configuracion,
            proveedor.GetRequiredService<IServicioCodigoBarras>(), NullLogger<ServicioImpresion>.Instance);

        var muestras = Environment.GetEnvironmentVariable("PAPELSOFT_MUESTRAS");

        foreach (var esCopia in new[] { false, true })
        {
            var titulo = esCopia ? "la tirilla de la copia se dibuja" : "la tirilla se dibuja";

            try
            {
                var tirilla = impresion.ConstruirTirilla(venta, esCopia);
                var ancho = 72 / 25.4 * 96;

                tirilla.Width = ancho;
                tirilla.Measure(new Size(ancho, double.PositiveInfinity));
                tirilla.Arrange(new Rect(tirilla.DesiredSize));
                tirilla.UpdateLayout();

                Verificar(titulo, tirilla.ActualHeight > 300, $"alto {tirilla.ActualHeight}");

                if (!string.IsNullOrWhiteSpace(muestras))
                {
                    const int escala = 3;

                    var mapa = new RenderTargetBitmap(
                        (int)(ancho * escala), (int)(tirilla.ActualHeight * escala),
                        96 * escala, 96 * escala, PixelFormats.Pbgra32);
                    mapa.Render(tirilla);

                    var png = new PngBitmapEncoder();
                    png.Frames.Add(BitmapFrame.Create(mapa));

                    Directory.CreateDirectory(muestras);
                    using var archivo = File.Create(
                        Path.Combine(muestras, esCopia ? "tirilla-copia.png" : "tirilla.png"));
                    png.Save(archivo);
                }
            }
            catch (Exception ex)
            {
                Verificar(titulo, false, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        var documentos = proveedor.GetRequiredService<IServicioDocumentos>();

        foreach (var formato in new[] { FormatoFactura.Recibo80mm, FormatoFactura.Carta })
        {
            var destino = string.IsNullOrWhiteSpace(muestras)
                ? null
                : Path.Combine(muestras, $"factura-{formato}.pdf");

            try
            {
                var ruta = await documentos.GenerarFacturaAsync(venta, formato, destino).ConfigureAwait(false);
                Verificar($"el PDF {formato} se genera", new FileInfo(ruta).Length > 1_000);
            }
            catch (Exception ex)
            {
                Verificar($"el PDF {formato} se genera", false, $"{ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>Tres tarifas, un descuento y pago en efectivo: todo lo que la tirilla sabe mostrar.</summary>
    private static VentaDetalladaDto VentaDeMuestra()
    {
        var lineas = new List<LineaDocumentoDto>
        {
            Linea("Resma papel carta 75 g Reprograf", 1, 10_000m, 19m, 10m),
            Linea("Lápiz negro N.º 2 HB", 3, 800m, 0m, 0m),
            Linea("Cuaderno cosido 100 hojas cuadriculado", 2, 4_200m, 5m, 0m)
        };

        var subtotal = lineas.Sum(l => l.Subtotal);
        var descuento = lineas.Sum(l => l.ValorDescuento);
        var iva = lineas.Sum(l => l.ValorIva);
        var total = subtotal - descuento + iva;

        return new VentaDetalladaDto
        {
            NumeroFactura = "FV-000045",
            Fecha = new DateTime(2026, 10, 1, 15, 42, 0),
            ClienteNombre = "Consumidor final",
            ClienteDocumento = "222222222222",
            UsuarioNombre = "Administrador",
            Subtotal = subtotal,
            TotalDescuento = descuento,
            TotalIva = iva,
            Total = total,
            MetodoPago = MetodoPago.Efectivo,
            MontoRecibido = 50_000m,
            Cambio = 50_000m - total,
            Lineas = lineas
        };

        static LineaDocumentoDto Linea(string descripcion, decimal cantidad, decimal precio, decimal iva, decimal descuento)
        {
            var subtotal = Dinero.Redondear(cantidad * precio);
            var valorDescuento = Dinero.Porcentaje(subtotal, descuento);
            var valorIva = Dinero.Porcentaje(subtotal - valorDescuento, iva);

            return new LineaDocumentoDto
            {
                Descripcion = descripcion,
                UnidadAbreviatura = "UND",
                Cantidad = cantidad,
                ValorUnitario = precio,
                PorcentajeDescuento = descuento,
                PorcentajeIva = iva,
                ValorDescuento = valorDescuento,
                ValorIva = valorIva,
                Subtotal = subtotal,
                Total = subtotal - valorDescuento + valorIva
            };
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
