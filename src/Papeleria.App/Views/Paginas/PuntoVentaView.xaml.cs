using System.Windows.Controls;

namespace Papeleria.App.Views.Paginas;

/// <summary>
/// Vista del punto de venta. Al mostrarse deja el foco en el cuadro de búsqueda
/// para que el lector de código de barras funcione sin tocar el ratón.
/// </summary>
public partial class PuntoVentaView : UserControl
{
    public PuntoVentaView()
    {
        InitializeComponent();

        Loaded += (_, _) => CampoBusqueda.Focus();
        IsVisibleChanged += (_, args) =>
        {
            if (args.NewValue is true)
            {
                CampoBusqueda.Focus();
            }
        };
    }
}
