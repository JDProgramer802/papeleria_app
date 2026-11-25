using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Papeleria.App.Infrastructure;
using Papeleria.Business.Security;
using Papeleria.Business.Services;
using Papeleria.Data.Seed;
using Papeleria.Domain.Exceptions;

namespace Papeleria.App.ViewModels;

/// <summary>Modelo de vista de la pantalla de inicio de sesión.</summary>
public partial class LoginVistaModelo : VistaModeloBase
{
    private readonly IServicioAutenticacion _autenticacion;
    private readonly IContextoSesion _sesion;
    private readonly IServicioConfiguracion _configuracion;
    private readonly IServicioRecuperacionAcceso _recuperacion;

    public LoginVistaModelo(
        IServicioAutenticacion autenticacion,
        IContextoSesion sesion,
        IServicioConfiguracion configuracion,
        IServicioRecuperacionAcceso recuperacion)
    {
        _autenticacion = autenticacion;
        _sesion = sesion;
        _configuracion = configuracion;
        _recuperacion = recuperacion;

        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
    }

    /// <summary>La ventana escucha este evento para cerrarse con resultado afirmativo.</summary>
    public event EventHandler? AutenticacionCorrecta;

    /// <summary>
    /// Segundo paso obligatorio: quien entra con la contraseña de fábrica tiene que
    /// ponerle una propia antes de llegar al programa.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmarContrasenaNuevaCommand))]
    [NotifyPropertyChangedFor(nameof(MostrandoAcceso))]
    private bool _exigiendoCambioContrasena;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmarContrasenaNuevaCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestablecerConCodigoCommand))]
    private string _contrasenaNueva = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmarContrasenaNuevaCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestablecerConCodigoCommand))]
    private string _contrasenaRepetida = string.Empty;

    /// <summary>
    /// «¿Olvidó su contraseña?»: el usuario escribe su código de recuperación y una
    /// contraseña nueva. Comparte los dos campos de contraseña nueva con el cambio
    /// obligatorio; nunca están los dos pasos a la vez.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrandoAcceso))]
    [NotifyPropertyChangedFor(nameof(PidiendoCodigo))]
    private bool _recuperando;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestablecerConCodigoCommand))]
    private string _codigoRecuperacion = string.Empty;

    /// <summary>El código nuevo que reemplaza al que se acaba de gastar; se muestra una sola vez.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PidiendoCodigo))]
    [NotifyPropertyChangedFor(nameof(MostrandoCodigoNuevo))]
    private string _codigoNuevo = string.Empty;

    /// <summary>El formulario normal de usuario y contraseña.</summary>
    public bool MostrandoAcceso => !ExigiendoCambioContrasena && !Recuperando;

    public bool PidiendoCodigo => Recuperando && string.IsNullOrEmpty(CodigoNuevo);

    public bool MostrandoCodigoNuevo => Recuperando && !string.IsNullOrEmpty(CodigoNuevo);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IniciarSesionCommand))]
    private string _nombreUsuario = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IniciarSesionCommand))]
    private string _contrasena = string.Empty;

    [ObservableProperty]
    private bool _recordarUsuario = true;

    /// <summary>Alterna entre el campo enmascarado y el de texto plano (el «ojito»).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoAyudaVisibilidad))]
    private bool _mostrarContrasena;

    /// <summary>Aviso de Bloq Mayús: la causa más habitual de un login fallido.</summary>
    [ObservableProperty]
    private bool _bloqueoMayusculasActivo;

    public string TextoAyudaVisibilidad =>
        MostrarContrasena ? "Ocultar la contraseña" : "Mostrar la contraseña";

    [ObservableProperty]
    private string _version = "1.0.0";

    [ObservableProperty]
    private string _nombreEmpresa = "Mi Papelería";

    /// <summary>Aviso que se muestra mientras el administrador conserve la clave de fábrica.</summary>
    [ObservableProperty]
    private bool _mostrarAvisoContrasenaPorDefecto;

    [ObservableProperty]
    private string _avisoContrasenaPorDefecto = string.Empty;

    public override async Task CargarAsync()
    {
        NombreEmpresa = _configuracion.ObtenerEmpresa().Nombre;
        RecordarUsuario = _autenticacion.RecordarUsuarioActivo();

        if (RecordarUsuario)
        {
            NombreUsuario = _autenticacion.ObtenerUltimoUsuario();
        }

        await ComprobarContrasenaPorDefectoAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Si el administrador sigue con la contraseña inicial, se muestra en pantalla
    /// para que el negocio pueda entrar la primera vez y se le recuerde cambiarla.
    /// </summary>
    private async Task ComprobarContrasenaPorDefectoAsync()
    {
        try
        {
            var esPorDefecto = await _autenticacion
                .UsaContrasenaPorDefectoAsync(SembradorDatos.UsuarioAdministrador)
                .ConfigureAwait(true);

            if (!esPorDefecto)
            {
                MostrarAvisoContrasenaPorDefecto = false;
                return;
            }

            MostrarAvisoContrasenaPorDefecto = true;
            AvisoContrasenaPorDefecto =
                $"Primer ingreso: usuario «{SembradorDatos.UsuarioAdministrador}» " +
                $"y contraseña «{SembradorDatos.ContrasenaAdministradorPorDefecto}». " +
                "Cámbiela desde Usuarios en cuanto entre.";

            if (string.IsNullOrWhiteSpace(NombreUsuario))
            {
                NombreUsuario = SembradorDatos.UsuarioAdministrador;
            }
        }
        catch (Exception)
        {
            // El aviso es una ayuda, no un requisito: si falla, el login sigue disponible.
            MostrarAvisoContrasenaPorDefecto = false;
        }
    }

    private bool PuedeIniciarSesion() =>
        !string.IsNullOrWhiteSpace(NombreUsuario) &&
        !string.IsNullOrWhiteSpace(Contrasena) &&
        !EstaCargando;

    private bool PuedeConfirmarContrasenaNueva() =>
        ExigiendoCambioContrasena &&
        !string.IsNullOrWhiteSpace(ContrasenaNueva) &&
        !string.IsNullOrWhiteSpace(ContrasenaRepetida) &&
        !EstaCargando;

    /// <summary>Guarda la contraseña propia y recién ahí deja entrar.</summary>
    [RelayCommand(CanExecute = nameof(PuedeConfirmarContrasenaNueva))]
    private async Task ConfirmarContrasenaNuevaAsync()
    {
        MensajeError = null;

        if (!string.Equals(ContrasenaNueva, ContrasenaRepetida, StringComparison.Ordinal))
        {
            MensajeError = "Las dos contraseñas no coinciden.";
            return;
        }

        EstaCargando = true;
        ConfirmarContrasenaNuevaCommand.NotifyCanExecuteChanged();

        try
        {
            await _autenticacion.CambiarContrasenaAsync(
                _sesion.UsuarioIdRequerido,
                SembradorDatos.ContrasenaAdministradorPorDefecto,
                ContrasenaNueva).ConfigureAwait(true);

            LimpiarCambioContrasena();

            AutenticacionCorrecta?.Invoke(this, EventArgs.Empty);
        }
        catch (NegocioException ex)
        {
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error inesperado al cambiar la contraseña de fábrica");
            MensajeError = "No se pudo guardar la contraseña. Revise el registro de errores.";
        }
        finally
        {
            EstaCargando = false;
            ConfirmarContrasenaNuevaCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Volver atrás cierra la sesión a medio abrir: nadie entra sin cambiarla.</summary>
    [RelayCommand]
    private void CancelarCambioContrasena()
    {
        _sesion.Cerrar();
        LimpiarCambioContrasena();

        MensajeError = null;
        MostrarAvisoContrasenaPorDefecto = true;
    }

    private void LimpiarCambioContrasena()
    {
        ExigiendoCambioContrasena = false;
        ContrasenaNueva = string.Empty;
        ContrasenaRepetida = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(PuedeIniciarSesion))]
    private async Task IniciarSesionAsync()
    {
        EstaCargando = true;
        MensajeError = null;
        IniciarSesionCommand.NotifyCanExecuteChanged();

        try
        {
            var usuario = await _autenticacion
                .AutenticarAsync(NombreUsuario, Contrasena)
                .ConfigureAwait(true);

            _sesion.Iniciar(usuario);

            await _autenticacion
                .GuardarPreferenciaUsuarioAsync(usuario.NombreUsuario, RecordarUsuario)
                .ConfigureAwait(true);

            var esDeFabrica = string.Equals(
                Contrasena, SembradorDatos.ContrasenaAdministradorPorDefecto, StringComparison.Ordinal);

            // La contraseña deja de estar en memoria en cuanto se valida.
            Contrasena = string.Empty;
            MostrarContrasena = false;

            // Con la contraseña de fábrica no se entra: la conoce cualquiera que haya
            // visto el programa, y de nada sirve todo lo demás si la puerta está abierta.
            if (esDeFabrica)
            {
                ExigiendoCambioContrasena = true;
                MostrarAvisoContrasenaPorDefecto = false;
                return;
            }

            AutenticacionCorrecta?.Invoke(this, EventArgs.Empty);
        }
        catch (NegocioException ex)
        {
            MensajeError = ex.Message;
            Contrasena = string.Empty;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error inesperado al iniciar sesión");
            MensajeError = "No se pudo validar el acceso. Revise el registro de errores.";
        }
        finally
        {
            EstaCargando = false;
            IniciarSesionCommand.NotifyCanExecuteChanged();
        }
    }

    // ── Recuperación con código ─────────────────────────────────────────────

    [RelayCommand]
    private void IniciarRecuperacion()
    {
        MensajeError = null;
        Contrasena = string.Empty;
        MostrarContrasena = false;
        CodigoRecuperacion = string.Empty;
        CodigoNuevo = string.Empty;
        ContrasenaNueva = string.Empty;
        ContrasenaRepetida = string.Empty;
        Recuperando = true;
    }

    private bool PuedeRestablecerConCodigo() =>
        Recuperando &&
        !string.IsNullOrWhiteSpace(NombreUsuario) &&
        !string.IsNullOrWhiteSpace(CodigoRecuperacion) &&
        !string.IsNullOrWhiteSpace(ContrasenaNueva) &&
        !string.IsNullOrWhiteSpace(ContrasenaRepetida) &&
        !EstaCargando;

    [RelayCommand(CanExecute = nameof(PuedeRestablecerConCodigo))]
    private async Task RestablecerConCodigoAsync()
    {
        MensajeError = null;

        if (!string.Equals(ContrasenaNueva, ContrasenaRepetida, StringComparison.Ordinal))
        {
            MensajeError = "Las dos contraseñas no coinciden.";
            return;
        }

        EstaCargando = true;
        RestablecerConCodigoCommand.NotifyCanExecuteChanged();

        try
        {
            CodigoNuevo = await _recuperacion
                .RestablecerConCodigoAsync(NombreUsuario, CodigoRecuperacion, ContrasenaNueva)
                .ConfigureAwait(true);

            CodigoRecuperacion = string.Empty;
            ContrasenaNueva = string.Empty;
            ContrasenaRepetida = string.Empty;
        }
        catch (NegocioException ex)
        {
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error inesperado al recuperar la contraseña");
            MensajeError = "No se pudo restablecer la contraseña. Revise el registro de errores.";
        }
        finally
        {
            EstaCargando = false;
            RestablecerConCodigoCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void CopiarCodigoNuevo()
    {
        try
        {
            System.Windows.Clipboard.SetText(CodigoNuevo);
        }
        catch (Exception)
        {
            // Otro programa puede tener el portapapeles tomado; el código sigue en pantalla.
        }
    }

    /// <summary>Vuelve al acceso normal. Si se acaba de restablecer, deja el aviso de que ya puede entrar.</summary>
    [RelayCommand]
    private void TerminarRecuperacion()
    {
        var restablecida = !string.IsNullOrEmpty(CodigoNuevo);

        Recuperando = false;
        CodigoRecuperacion = string.Empty;
        CodigoNuevo = string.Empty;
        ContrasenaNueva = string.Empty;
        ContrasenaRepetida = string.Empty;
        MensajeError = null;

        if (restablecida)
        {
            MostrarAvisoContrasenaPorDefecto = true;
            AvisoContrasenaPorDefecto = "Contraseña restablecida. Entre con la nueva.";
        }
    }

    [RelayCommand]
    private static void Salir() => System.Windows.Application.Current.Shutdown();
}
