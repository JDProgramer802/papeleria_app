using System.Windows;
using System.Windows.Controls;
using Papeleria.App.ViewModels.Dialogos;

namespace Papeleria.App.Views.Dialogos;

/// <summary>Vista del formulario de usuarios del sistema.</summary>
public partial class UsuarioDialogoView : UserControl
{
    public UsuarioDialogoView() => InitializeComponent();

    /// <summary>
    /// El <c>PasswordBox</c> no expone su contenido como propiedad enlazable por
    /// seguridad, así que la contraseña se traslada al modelo desde su propio evento.
    /// </summary>
    private void ContrasenaCambiada(object remitente, RoutedEventArgs argumentos)
    {
        if (DataContext is UsuarioDialogoVistaModelo modelo && remitente is PasswordBox campo)
        {
            modelo.Contrasena = campo.Password;
        }
    }

    private void ConfirmacionCambiada(object remitente, RoutedEventArgs argumentos)
    {
        if (DataContext is UsuarioDialogoVistaModelo modelo && remitente is PasswordBox campo)
        {
            modelo.ConfirmacionContrasena = campo.Password;
        }
    }
}
