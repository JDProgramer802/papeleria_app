using System.Windows;

namespace Papeleria.App.Views;

/// <summary>
/// Ventana principal del sistema. Aloja el menú lateral, la barra superior,
/// el panel de contenido y el host de diálogos compartido por toda la aplicación.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}
