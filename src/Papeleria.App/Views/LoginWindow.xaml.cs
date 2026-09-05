using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Papeleria.App.ViewModels;

namespace Papeleria.App.Views;

/// <summary>Ventana de inicio de sesión.</summary>
public partial class LoginWindow : Window
{
    /// <summary>
    /// Evita el bucle de notificaciones al copiar la contraseña entre el campo
    /// enmascarado y el modelo de vista.
    /// </summary>
    private bool _sincronizando;

    public LoginWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, args) =>
        {
            if (args.OldValue is LoginVistaModelo anterior)
            {
                anterior.AutenticacionCorrecta -= CerrarConExito;
                anterior.PropertyChanged -= AlCambiarPropiedadDelModelo;
            }

            if (args.NewValue is LoginVistaModelo nuevo)
            {
                nuevo.AutenticacionCorrecta += CerrarConExito;
                nuevo.PropertyChanged += AlCambiarPropiedadDelModelo;
            }
        };

        Loaded += (_, _) =>
        {
            ActualizarBloqueoMayusculas();

            // Si ya se recordó el usuario, el foco va directo a la contraseña.
            if (DataContext is LoginVistaModelo modelo && !string.IsNullOrWhiteSpace(modelo.NombreUsuario))
            {
                CampoContrasena.Focus();
            }
            else
            {
                CampoUsuario.Focus();
            }
        };
    }

    /// <summary>
    /// Recorta el contenido con las esquinas redondeadas de la ventana.
    ///
    /// <c>ClipToBounds</c> recorta al rectángulo, no al radio: sin esto el degradado
    /// del fondo se desborda por las cuatro esquinas y la ventana se ve cuadrada
    /// aunque el marco esté redondeado.
    /// </summary>
    private void AjustarRecorte(object remitente, SizeChangedEventArgs argumentos)
    {
        if (remitente is not Border marco)
        {
            return;
        }

        var radio = marco.CornerRadius.TopLeft;

        marco.Clip = new RectangleGeometry(
            new Rect(0, 0, marco.ActualWidth, marco.ActualHeight), radio, radio);
    }

    private void CerrarConExito(object? remitente, EventArgs argumentos)
    {
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// El modelo de vista puede vaciar la contraseña (por ejemplo tras un intento
    /// fallido); hay que reflejarlo en el campo enmascarado, que no es enlazable.
    /// </summary>
    private void AlCambiarPropiedadDelModelo(object? remitente, System.ComponentModel.PropertyChangedEventArgs argumentos)
    {
        if (DataContext is not LoginVistaModelo modelo)
        {
            return;
        }

        // Al entrar o salir del cambio obligatorio se vacían los campos enmascarados
        // y el foco va donde toca escribir.
        if (argumentos.PropertyName == nameof(LoginVistaModelo.ExigiendoCambioContrasena))
        {
            CampoContrasenaNueva.Password = string.Empty;
            CampoContrasenaRepetida.Password = string.Empty;

            if (modelo.ExigiendoCambioContrasena)
            {
                Dispatcher.BeginInvoke(new Action(() => CampoContrasenaNueva.Focus()));
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() => CampoContrasena.Focus()));
            }

            return;
        }

        if (argumentos.PropertyName != nameof(LoginVistaModelo.Contrasena) || _sincronizando)
        {
            return;
        }

        if (CampoContrasena.Password != modelo.Contrasena)
        {
            _sincronizando = true;
            CampoContrasena.Password = modelo.Contrasena;
            _sincronizando = false;
        }
    }

    /// <summary>
    /// El <c>PasswordBox</c> no expone su contenido como propiedad enlazable por
    /// seguridad, así que se traslada al modelo de vista desde su propio evento.
    /// </summary>
    private void ContrasenaCambiada(object remitente, RoutedEventArgs argumentos)
    {
        if (_sincronizando || DataContext is not LoginVistaModelo modelo)
        {
            return;
        }

        _sincronizando = true;
        modelo.Contrasena = CampoContrasena.Password;
        _sincronizando = false;
    }

    /// <summary>
    /// Los dos campos del cambio obligatorio de contraseña. Como el resto de
    /// <c>PasswordBox</c>, no se enlazan: se trasladan a mano al modelo de vista.
    /// </summary>
    private void ContrasenaNuevaCambiada(object remitente, RoutedEventArgs argumentos)
    {
        if (DataContext is LoginVistaModelo modelo)
        {
            modelo.ContrasenaNueva = CampoContrasenaNueva.Password;
        }
    }

    private void ContrasenaRepetidaCambiada(object remitente, RoutedEventArgs argumentos)
    {
        if (DataContext is LoginVistaModelo modelo)
        {
            modelo.ContrasenaRepetida = CampoContrasenaRepetida.Password;
        }
    }

    /// <summary>
    /// Al pulsar el «ojito» se cambia de campo: hay que llevar el foco al que queda
    /// visible y dejar el cursor al final para poder seguir escribiendo sin interrupción.
    /// </summary>
    private void VisibilidadContrasenaCambiada(object remitente, RoutedEventArgs argumentos)
    {
        if (DataContext is not LoginVistaModelo modelo)
        {
            return;
        }

        if (modelo.MostrarContrasena)
        {
            CampoContrasenaVisible.Focus();
            CampoContrasenaVisible.CaretIndex = CampoContrasenaVisible.Text.Length;
            return;
        }

        _sincronizando = true;
        CampoContrasena.Password = modelo.Contrasena;
        _sincronizando = false;

        CampoContrasena.Focus();
    }

    private void ComprobarBloqueoMayusculas(object remitente, KeyEventArgs argumentos) =>
        ActualizarBloqueoMayusculas();

    private void ActualizarBloqueoMayusculas()
    {
        if (DataContext is LoginVistaModelo modelo)
        {
            modelo.BloqueoMayusculasActivo = Keyboard.IsKeyToggled(Key.CapsLock);
        }
    }

    private void ArrastrarVentana(object remitente, MouseButtonEventArgs argumentos)
    {
        if (argumentos.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void MinimizarVentana(object remitente, RoutedEventArgs argumentos) =>
        WindowState = WindowState.Minimized;
}
