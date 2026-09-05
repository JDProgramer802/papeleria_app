using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Papeleria.App.Infrastructure;
using Papeleria.App.ViewModels;
using Papeleria.App.ViewModels.Paginas;
using Papeleria.App.Views;
using Papeleria.Business.DependencyInjection;
using Papeleria.Business.Services;
using Papeleria.Data;
using Papeleria.Data.Storage;
using Papeleria.Domain.Exceptions;
using Serilog;
using Serilog.Events;

namespace Papeleria.App;

/// <summary>
/// Punto de entrada de la aplicación. Construye el contenedor de dependencias,
/// prepara la base de datos mostrando la pantalla de carga y encadena
/// splash → inicio de sesión → ventana principal.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Semáforo que impide dos copias abiertas a la vez. Dos procesos sobre la misma
    /// base de datos pueden repetir el consecutivo de factura o pelearse por la caja
    /// abierta, así que la segunda copia se cierra avisando al usuario.
    /// </summary>
    private const string NombreInstanciaUnica = @"Local\PapeleriaApp.InstanciaUnica";

    private IHost? _anfitrion;
    private Mutex? _instanciaUnica;

    public App()
    {
        // Red de seguridad instalada antes de que WPF cargue los diccionarios de
        // recursos: si algo falla tan temprano, Serilog todavía no existe y sin este
        // volcado el proceso moriría sin dejar rastro del motivo.
        AppDomain.CurrentDomain.FirstChanceException += AlPrimeraExcepcion;
    }

    private static bool _falloRegistrado;

    private static void AlPrimeraExcepcion(object? remitente, FirstChanceExceptionEventArgs argumentos) =>
        RegistrarFalloTemprano(argumentos.Exception);

    /// <summary>Desactiva el volcado temprano en cuanto Serilog toma el relevo.</summary>
    private void DejarDeVigilarArranque() =>
        AppDomain.CurrentDomain.FirstChanceException -= AlPrimeraExcepcion;

    private static void RegistrarFalloTemprano(Exception excepcion)
    {
        if (_falloRegistrado)
        {
            return;
        }

        try
        {
            _falloRegistrado = true;

            var carpeta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PapeleriaApp", "Logs");

            Directory.CreateDirectory(carpeta);

            File.AppendAllText(
                Path.Combine(carpeta, "arranque-error.txt"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {excepcion}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Si ni esto se puede escribir, no hay nada más que hacer.
        }
    }

    /// <summary>Contenedor de servicios accesible desde toda la aplicación.</summary>
    public static IServiceProvider Servicios =>
        ((App)Current)._anfitrion?.Services
        ?? throw new InvalidOperationException("La aplicación aún no ha terminado de iniciarse.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Se comprueba antes que nada: la segunda copia debe salir sin tocar
        // siquiera los archivos de registro de la primera.
        if (!TomarInstanciaUnica())
        {
            MessageBox.Show(
                "El sistema ya está abierto." + Environment.NewLine + Environment.NewLine +
                "Utilice la ventana que ya está en marcha. Trabajar con dos copias a la vez " +
                "puede duplicar números de factura y descuadrar la caja.",
                "PapelSoft",
                MessageBoxButton.OK, MessageBoxImage.Information);

            Shutdown();
            return;
        }

        RutasAplicacion.AsegurarCarpetas();
        ConfigurarRegistroDeEventos();
        RegistrarManejoGlobalDeErrores();

        // A partir de aquí el registro lo lleva Serilog.
        DejarDeVigilarArranque();

        // La licencia Community de QuestPDF debe declararse antes de generar documentos.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        _anfitrion = ConstruirAnfitrion();
        await _anfitrion.StartAsync().ConfigureAwait(true);

        // Sin esto, cerrar la pantalla de carga daría por terminada la aplicación.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            var splash = new SplashWindow();
            splash.Show();

            var preparado = await PrepararAplicacionAsync(splash).ConfigureAwait(true);

            splash.Close();

            if (!preparado)
            {
                Shutdown(1);
                return;
            }

            await MostrarInicioDeSesionAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fallo irrecuperable durante el arranque");

            MessageBox.Show(
                "No fue posible iniciar el sistema.\n\n" + ex.Message +
                $"\n\nRevise el registro en:\n{RutasAplicacion.CarpetaLogs}",
                "Error de arranque", MessageBoxButton.OK, MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    /// <summary>
    /// Reserva el semáforo de instancia única. Devuelve <c>false</c> si otra copia
    /// del programa ya lo tiene tomado en esta sesión de Windows.
    /// </summary>
    private bool TomarInstanciaUnica()
    {
        try
        {
            _instanciaUnica = new Mutex(true, NombreInstanciaUnica, out var creado);

            if (creado)
            {
                return true;
            }

            _instanciaUnica.Dispose();
            _instanciaUnica = null;

            return false;
        }
        catch (Exception)
        {
            // Si el sistema no deja crear el semáforo, es preferible arrancar
            // a dejar al usuario sin programa.
            return true;
        }
    }

    /// <summary>Ejecuta la inicialización reportando el avance a la pantalla de carga.</summary>
    private async Task<bool> PrepararAplicacionAsync(SplashWindow splash)
    {
        var progreso = new Progress<AvanceInicializacion>(avance =>
            splash.ActualizarProgreso(avance.Mensaje, avance.Porcentaje));

        try
        {
            var inicializador = Servicios.GetRequiredService<IInicializadorBaseDatos>();
            await inicializador.InicializarAsync(progreso).ConfigureAwait(true);

            var configuracion = Servicios.GetRequiredService<IServicioConfiguracion>();
            await configuracion.CargarAsync().ConfigureAwait(true);

            // Borra el ejecutable anterior si en la sesión previa se aplicó una actualización.
            Servicios.GetRequiredService<IServicioActualizaciones>().LimpiarRestosDeActualizacion();

            Servicios.GetRequiredService<IServicioTema>().AplicarTemaGuardado();

            splash.ActualizarProgreso("Todo listo.", 100);

            // Pausa breve para que la animación de la barra termine de forma natural.
            await Task.Delay(400).ConfigureAwait(true);

            return true;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Error preparando la base de datos");

            MessageBox.Show(
                "No se pudo preparar la base de datos.\n\n" + ex.Message +
                $"\n\nUbicación de los datos:\n{RutasAplicacion.ArchivoBaseDatos}",
                "Error de base de datos", MessageBoxButton.OK, MessageBoxImage.Error);

            return false;
        }
    }

    /// <summary>Muestra el login y, si la autenticación tiene éxito, abre la ventana principal.</summary>
    private async Task MostrarInicioDeSesionAsync()
    {
        var vistaModeloLogin = Servicios.GetRequiredService<LoginVistaModelo>();
        await vistaModeloLogin.CargarAsync().ConfigureAwait(true);

        var ventanaLogin = new LoginWindow { DataContext = vistaModeloLogin };

        if (ventanaLogin.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        var principal = new MainWindow
        {
            DataContext = Servicios.GetRequiredService<MainVistaModelo>()
        };

        MainWindow = principal;
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        principal.Show();

        await ((MainVistaModelo)principal.DataContext).IniciarAsync().ConfigureAwait(true);
    }

    private static IHost ConstruirAnfitrion() =>
        Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, servicios) =>
            {
                servicios.AgregarCapaNegocio();

                // Infraestructura de presentación
                servicios.AddSingleton<ISnackbarMessageQueue>(
                    _ => new SnackbarMessageQueue(TimeSpan.FromSeconds(4)));
                servicios.AddSingleton<IServicioDialogos, ServicioDialogos>();
                servicios.AddSingleton<IServicioArchivos, ServicioArchivos>();
                servicios.AddSingleton<IServicioTema, ServicioTema>();
                servicios.AddSingleton<Impresion.IServicioImpresion, Impresion.ServicioImpresion>();
                servicios.AddSingleton<INavegacion, Navegacion>();

                // Modelos de vista principales
                servicios.AddSingleton<MainVistaModelo>();
                servicios.AddTransient<LoginVistaModelo>();

                // Páginas: el servicio de navegación conserva la instancia de cada módulo.
                servicios.AddTransient<DashboardVistaModelo>();
                servicios.AddTransient<ProductosVistaModelo>();
                servicios.AddTransient<CatalogosVistaModelo>();
                servicios.AddTransient<ProveedoresVistaModelo>();
                servicios.AddTransient<ClientesVistaModelo>();
                servicios.AddTransient<ComprasVistaModelo>();
                servicios.AddTransient<PuntoVentaVistaModelo>();
                servicios.AddTransient<HistorialVentasVistaModelo>();
                servicios.AddTransient<CotizacionesVistaModelo>();
                servicios.AddTransient<CarteraVistaModelo>();
                servicios.AddTransient<InventarioVistaModelo>();
                servicios.AddTransient<KardexVistaModelo>();
                servicios.AddTransient<CajaVistaModelo>();
                servicios.AddTransient<ReportesVistaModelo>();
                servicios.AddTransient<ConfiguracionVistaModelo>();
                servicios.AddTransient<UsuariosVistaModelo>();
                servicios.AddTransient<ManualVistaModelo>();

                // Los modelos de vista de los diálogos los construye la página que los
                // abre, porque reciben la entidad que se está editando como parámetro.
            })
            .Build();

    private static void ConfigurarRegistroDeEventos()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(RutasAplicacion.CarpetaLogs, "papeleria-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("=== PapelSoft iniciado ===");
    }

    /// <summary>
    /// Captura las excepciones que escapan de la interfaz, de las tareas en segundo plano
    /// y del dominio, para registrarlas y avisar al usuario sin cerrar el programa.
    /// </summary>
    private void RegistrarManejoGlobalDeErrores()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            NotificarError(args.Exception, "interfaz");
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
            Log.Error(args.Exception, "Excepción no observada en una tarea en segundo plano");
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception excepcion)
            {
                Log.Fatal(excepcion, "Excepción no controlada en el dominio de la aplicación");
                Log.CloseAndFlush();
            }
        };
    }

    private static void NotificarError(Exception excepcion, string origen)
    {
        // Los errores de negocio son previsibles: se muestran tal cual, sin traza técnica.
        if (excepcion is NegocioException)
        {
            Log.Warning("Regla de negocio: {Mensaje}", excepcion.Message);

            MessageBox.Show(excepcion.Message, "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);

            return;
        }

        Log.Error(excepcion, "Error no controlado ({Origen})", origen);

        MessageBox.Show(
            "Ocurrió un error inesperado. La operación no se completó.\n\n" +
            excepcion.Message +
            $"\n\nSe registró el detalle en:\n{RutasAplicacion.CarpetaLogs}",
            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            // Copia de seguridad programada al cerrar, si la configuración lo pide.
            if (_anfitrion is not null)
            {
                var respaldo = _anfitrion.Services.GetRequiredService<IServicioBackup>();
                await respaldo.EjecutarProgramadoAsync().ConfigureAwait(false);

                await _anfitrion.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                _anfitrion.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error durante el cierre de la aplicación");
        }
        finally
        {
            if (_instanciaUnica is not null)
            {
                _instanciaUnica.ReleaseMutex();
                _instanciaUnica.Dispose();
                _instanciaUnica = null;
            }

            Log.Information("=== Sistema finalizado ===");
            Log.CloseAndFlush();
        }

        base.OnExit(e);
    }
}
