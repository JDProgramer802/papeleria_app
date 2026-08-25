using System.IO;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MaterialDesignThemes.Wpf;
using Papeleria.App.Infrastructure;
using Papeleria.App.ViewModels.Dialogos;
using Papeleria.Business.Common;
using Papeleria.Business.Security;
using Papeleria.Business.Services;
using Papeleria.Domain.Constants;

namespace Papeleria.App.ViewModels;

/// <summary>Entrada del menú lateral.</summary>
public partial class ElementoMenu : ObservableObject
{
    public required string Modulo { get; init; }

    public required string Titulo { get; init; }

    public required PackIconKind Icono { get; init; }

    public required string Descripcion { get; init; }

    [ObservableProperty]
    private bool _estaSeleccionado;
}

/// <summary>
/// Modelo de vista de la ventana principal: menú lateral, barra superior,
/// panel de contenido y barra de estado.
/// </summary>
public partial class MainVistaModelo : ObservableObject
{
    private readonly INavegacion _navegacion;
    private readonly IContextoSesion _sesion;
    private readonly IServicioTema _tema;
    private readonly IServicioCaja _caja;
    private readonly IServicioConfiguracion _configuracion;
    private readonly IServicioDialogos _dialogos;
    private readonly IServicioBackup _respaldo;
    private readonly IServicioActualizaciones _actualizaciones;
    private readonly DispatcherTimer _reloj;

    public MainVistaModelo(
        INavegacion navegacion,
        IContextoSesion sesion,
        IServicioTema tema,
        IServicioCaja caja,
        IServicioConfiguracion configuracion,
        IServicioDialogos dialogos,
        IServicioBackup respaldo,
        IServicioActualizaciones actualizaciones,
        ISnackbarMessageQueue colaMensajes)
    {
        _navegacion = navegacion;
        _sesion = sesion;
        _tema = tema;
        _caja = caja;
        _configuracion = configuracion;
        _dialogos = dialogos;
        _respaldo = respaldo;
        _actualizaciones = actualizaciones;

        ColaMensajes = colaMensajes;
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        _navegacion.Navegado += AlNavegar;
        _tema.TemaCambiado += (_, _) => EsTemaOscuro = _tema.EsOscuro;

        _reloj = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _reloj.Tick += (_, _) => ActualizarReloj();

        RegistrarSuscripciones();
    }

    public ISnackbarMessageQueue ColaMensajes { get; }

    public ObservableCollection<ElementoMenu> Menu { get; } = new();

    [ObservableProperty]
    private PaginaVistaModelo? _paginaActual;

    [ObservableProperty]
    private bool _menuColapsado;

    [ObservableProperty]
    private bool _esTemaOscuro;

    [ObservableProperty]
    private string _version = "1.0.0";

    [ObservableProperty]
    private string _nombreEmpresa = "Mi Papelería";

    /// <summary>
    /// Ruta del logotipo cargado en Configuración. Queda vacía si no hay ninguno o si
    /// el archivo ya no existe, y entonces el menú muestra la marca por defecto.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneLogo))]
    private string _logoEmpresa = string.Empty;

    public bool TieneLogo => !string.IsNullOrWhiteSpace(LogoEmpresa);

    [ObservableProperty]
    private string _nombreUsuario = string.Empty;

    [ObservableProperty]
    private string _rolUsuario = string.Empty;

    [ObservableProperty]
    private string _inicialesUsuario = "?";

    [ObservableProperty]
    private string _fechaActual = string.Empty;

    [ObservableProperty]
    private string _horaActual = string.Empty;

    [ObservableProperty]
    private bool _cajaAbierta;

    [ObservableProperty]
    private string _estadoCaja = "Caja cerrada";

    [ObservableProperty]
    private string _tituloPaginaActual = "Dashboard";

    /// <summary>Prepara la sesión: menú, datos del usuario, reloj y página inicial.</summary>
    public async Task IniciarAsync()
    {
        var usuario = _sesion.Usuario;

        if (usuario is not null)
        {
            NombreUsuario = usuario.NombreCompleto;
            RolUsuario = usuario.RolTexto;
            InicialesUsuario = usuario.Iniciales;
        }

        ActualizarMarcaEmpresa();
        EsTemaOscuro = _tema.EsOscuro;
        MenuColapsado = _configuracion.ObtenerBooleano(ClavesConfiguracion.MenuColapsado);

        ConstruirMenu();
        ActualizarReloj();
        _reloj.Start();

        await ActualizarEstadoCajaAsync().ConfigureAwait(true);

        // Se abre el primer módulo al que el usuario tenga acceso.
        var inicial = Menu.FirstOrDefault();

        if (inicial is not null)
        {
            await _navegacion.NavegarAsync(inicial.Modulo).ConfigureAwait(true);
        }

        // La comprobación de versión va después de abrir la pantalla y sin await:
        // si no hay internet o GitHub tarda, el usuario ya está trabajando.
        _ = ComprobarActualizacionesEnSegundoPlanoAsync();
    }

    private async Task ComprobarActualizacionesEnSegundoPlanoAsync()
    {
        try
        {
            var actualizacion = await _actualizaciones.ComprobarAsync().ConfigureAwait(true);

            if (actualizacion is null)
            {
                return;
            }

            await MostrarDialogoActualizacionAsync(actualizacion).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // Nunca debe estorbar: si falla, se registra y el negocio sigue.
            Serilog.Log.Information(ex, "No se pudo comprobar si hay actualizaciones");
        }
    }

    private async Task MostrarDialogoActualizacionAsync(ActualizacionDisponible actualizacion)
    {
        var impedimento = _actualizaciones.ComprobarViabilidad();

        // Si el equipo no puede aplicarla sola, al menos se avisa de que existe.
        if (impedimento != ImpedimentoActualizacion.Ninguno)
        {
            _dialogos.Notificar(
                $"Hay una versión nueva ({actualizacion.Version.ToString(3)}), " +
                "pero este equipo no puede instalarla automáticamente.");

            return;
        }

        var dialogo = new ActualizacionDialogoVistaModelo(_actualizaciones, _dialogos, actualizacion);
        await _dialogos.MostrarAsync(dialogo).ConfigureAwait(true);
    }

    /// <summary>Comprobación manual desde la barra superior o desde Configuración.</summary>
    [RelayCommand]
    private async Task BuscarActualizacionesAsync()
    {
        try
        {
            var actualizacion = await _actualizaciones.ComprobarAsync(forzar: true).ConfigureAwait(true);

            if (actualizacion is null)
            {
                var repositorio = _configuracion.ObtenerTexto(ClavesConfiguracion.ActualizacionesRepositorio);

                _dialogos.Notificar(string.IsNullOrWhiteSpace(repositorio)
                    ? "No hay un repositorio de actualizaciones configurado."
                    : $"Ya tiene la versión más reciente ({Version}).");

                return;
            }

            await MostrarDialogoActualizacionAsync(actualizacion).ConfigureAwait(true);
        }
        catch (Domain.Exceptions.NegocioException ex)
        {
            await _dialogos.InformarAsync("Actualizaciones", ex.Message, esError: true).ConfigureAwait(true);
        }
    }

    /// <summary>Arma el menú dejando solo los módulos permitidos al rol de la sesión.</summary>
    private void ConstruirMenu()
    {
        var elementos = new (string Modulo, string Titulo, PackIconKind Icono, string Descripcion)[]
        {
            (Modulos.Dashboard, "Dashboard", PackIconKind.ViewDashboardOutline, "Resumen del negocio"),
            (Modulos.Ventas, "Punto de venta", PackIconKind.CashRegister, "Registrar ventas"),
            (Modulos.HistorialVentas, "Historial de ventas", PackIconKind.ReceiptTextOutline,
                "Facturas emitidas y ventas del día"),
            (Modulos.Productos, "Productos", PackIconKind.PackageVariantClosed, "Catálogo de artículos"),
            (Modulos.Inventario, "Inventario", PackIconKind.Warehouse, "Existencias y movimientos"),
            (Modulos.Compras, "Compras", PackIconKind.TruckDeliveryOutline, "Compras a proveedores"),
            (Modulos.Cartera, "Cartera", PackIconKind.AccountCashOutline, "Cuentas por cobrar"),
            (Modulos.Caja, "Caja", PackIconKind.CashMultiple, "Apertura, arqueo y cierre"),
            (Modulos.Kardex, "Kardex", PackIconKind.SwapHorizontalBold, "Historial de inventario"),
            (Modulos.Clientes, "Clientes", PackIconKind.AccountGroupOutline, "Directorio de clientes"),
            (Modulos.Proveedores, "Proveedores", PackIconKind.DomainPlus, "Directorio de proveedores"),
            (Modulos.Catalogos, "Catálogos", PackIconKind.ShapeOutline, "Categorías, marcas y unidades"),
            (Modulos.Reportes, "Reportes", PackIconKind.ChartBoxOutline, "Informes y exportación"),
            (Modulos.Usuarios, "Usuarios", PackIconKind.AccountCogOutline, "Usuarios y permisos"),
            (Modulos.Configuracion, "Configuración", PackIconKind.CogOutline, "Empresa, impuestos y respaldos"),
            (Modulos.Manual, "Manual de uso", PackIconKind.BookOpenPageVariantOutline,
                "Guía de uso y tutorial guiado")
        };

        Menu.Clear();

        foreach (var elemento in elementos.Where(e => _navegacion.PuedeNavegar(e.Modulo)))
        {
            Menu.Add(new ElementoMenu
            {
                Modulo = elemento.Modulo,
                Titulo = elemento.Titulo,
                Icono = elemento.Icono,
                Descripcion = elemento.Descripcion
            });
        }
    }

    private void RegistrarSuscripciones()
    {
        var mensajero = WeakReferenceMessenger.Default;

        mensajero.Register<MainVistaModelo, CajaCambiadaMensaje>(this, (destinatario, mensaje) =>
        {
            _ = destinatario.ActualizarEstadoCajaAsync();
        });

        mensajero.Register<MainVistaModelo, VentaRegistradaMensaje>(this, (destinatario, mensaje) =>
            destinatario._dialogos.Notificar($"Venta {mensaje.Value} registrada correctamente."));

        mensajero.Register<MainVistaModelo, ConfiguracionCambiadaMensaje>(this, (destinatario, mensaje) =>
            destinatario.ActualizarMarcaEmpresa());

        mensajero.Register<MainVistaModelo, NavegarMensaje>(this, (destinatario, mensaje) =>
        {
            _ = destinatario.NavegarAsync(mensaje.Value);
        });
    }

    /// <summary>
    /// Refresca el nombre y el logotipo que muestra el menú lateral. Se llama al
    /// iniciar y cada vez que se guardan los datos de la empresa, para que el cambio
    /// se vea al momento sin reiniciar.
    /// </summary>
    private void ActualizarMarcaEmpresa()
    {
        var empresa = _configuracion.ObtenerEmpresa();

        NombreEmpresa = empresa.Nombre;

        // TieneLogo comprueba además que el archivo siga existiendo: si el usuario lo
        // borró del disco, se vuelve a la marca por defecto en lugar de un hueco.
        LogoEmpresa = empresa.TieneLogo ? empresa.LogoPath : string.Empty;
    }

    private async Task ActualizarEstadoCajaAsync()
    {
        try
        {
            var sesionCaja = await _caja.ObtenerSesionAbiertaAsync().ConfigureAwait(true);

            CajaAbierta = sesionCaja is not null;

            EstadoCaja = sesionCaja is null
                ? "Caja cerrada"
                : $"Caja abierta desde {Formatos.Hora(sesionCaja.FechaApertura)}";
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "No se pudo consultar el estado de la caja");
            EstadoCaja = "Estado de caja no disponible";
        }
    }

    private void ActualizarReloj()
    {
        var ahora = DateTime.Now;
        FechaActual = Formatos.FechaLarga(ahora);
        HoraActual = ahora.ToString("hh:mm:ss tt", Formatos.Cultura);
    }

    private void AlNavegar(object? remitente, PaginaVistaModelo pagina)
    {
        PaginaActual = pagina;
        TituloPaginaActual = pagina.Titulo;

        foreach (var elemento in Menu)
        {
            elemento.EstaSeleccionado = string.Equals(
                elemento.Modulo, pagina.Modulo, StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand]
    private async Task NavegarAsync(string? modulo)
    {
        if (!string.IsNullOrWhiteSpace(modulo))
        {
            await _navegacion.NavegarAsync(modulo).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task AlternarMenuAsync()
    {
        MenuColapsado = !MenuColapsado;

        await _configuracion
            .GuardarAsync(ClavesConfiguracion.MenuColapsado, MenuColapsado.ToString())
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private Task AlternarTemaAsync() => _tema.AlternarAsync();

    [RelayCommand]
    private Task RecargarAsync() => _navegacion.RecargarAsync();

    [RelayCommand]
    private async Task CrearRespaldoAsync()
    {
        try
        {
            var ruta = await _respaldo.CrearAsync().ConfigureAwait(true);
            _dialogos.Notificar($"Copia de seguridad creada: {Path.GetFileName(ruta)}");
        }
        catch (Domain.Exceptions.NegocioException ex)
        {
            await _dialogos.InformarAsync("Copia de seguridad", ex.Message, esError: true).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task CerrarSesionAsync()
    {
        var confirmado = await _dialogos.ConfirmarAsync(
            "Cerrar sesión",
            "¿Desea cerrar la sesión actual? Se cerrará la aplicación y podrá volver a entrar con otro usuario.",
            "Cerrar sesión").ConfigureAwait(true);

        if (!confirmado)
        {
            return;
        }

        _reloj.Stop();
        _sesion.Cerrar();

        // Reiniciar el proceso es la forma más segura de dejar el estado limpio
        // para el siguiente usuario, sin arrastrar datos en memoria de la sesión anterior.
        var ejecutable = Environment.ProcessPath;

        if (!string.IsNullOrWhiteSpace(ejecutable))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ejecutable)
            {
                UseShellExecute = true
            });
        }

        System.Windows.Application.Current.Shutdown();
    }

    [RelayCommand]
    private async Task SalirAsync()
    {
        var confirmado = await _dialogos.ConfirmarAsync(
            "Salir del sistema",
            "¿Desea cerrar el sistema de gestión?",
            "Salir").ConfigureAwait(true);

        if (confirmado)
        {
            _reloj.Stop();
            System.Windows.Application.Current.Shutdown();
        }
    }
}
