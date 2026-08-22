using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace Papeleria.App.Controls;

/// <summary>
/// Envoltorio común de los formularios en diálogo. Aporta la cabecera con icono, el
/// área desplazable de campos, el aviso de error y el pie de acciones, de modo que
/// todos los CRUD compartan la misma estructura y no haya que repetirla en cada vista.
/// La plantilla vive en <c>Resources/Controles.xaml</c>.
/// </summary>
public class PanelFormulario : ContentControl
{
    static PanelFormulario()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(PanelFormulario), new FrameworkPropertyMetadata(typeof(PanelFormulario)));
    }

    public static readonly DependencyProperty TituloProperty = DependencyProperty.Register(
        nameof(Titulo), typeof(string), typeof(PanelFormulario), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtituloProperty = DependencyProperty.Register(
        nameof(Subtitulo), typeof(string), typeof(PanelFormulario), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconoProperty = DependencyProperty.Register(
        nameof(Icono), typeof(PackIconKind), typeof(PanelFormulario),
        new PropertyMetadata(PackIconKind.FileDocumentEditOutline));

    public static readonly DependencyProperty PieProperty = DependencyProperty.Register(
        nameof(Pie), typeof(object), typeof(PanelFormulario), new PropertyMetadata(null));

    public static readonly DependencyProperty AltoMaximoContenidoProperty = DependencyProperty.Register(
        nameof(AltoMaximoContenido), typeof(double), typeof(PanelFormulario), new PropertyMetadata(560d));

    /// <summary>Muestra la leyenda que explica el asterisco de los campos obligatorios.</summary>
    public static readonly DependencyProperty MostrarLeyendaObligatoriosProperty = DependencyProperty.Register(
        nameof(MostrarLeyendaObligatorios), typeof(bool), typeof(PanelFormulario), new PropertyMetadata(true));

    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    public string Subtitulo
    {
        get => (string)GetValue(SubtituloProperty);
        set => SetValue(SubtituloProperty, value);
    }

    public PackIconKind Icono
    {
        get => (PackIconKind)GetValue(IconoProperty);
        set => SetValue(IconoProperty, value);
    }

    /// <summary>Botones de acción del formulario.</summary>
    public object? Pie
    {
        get => GetValue(PieProperty);
        set => SetValue(PieProperty, value);
    }

    /// <summary>Límite de alto del área de campos antes de que aparezca el desplazamiento.</summary>
    public double AltoMaximoContenido
    {
        get => (double)GetValue(AltoMaximoContenidoProperty);
        set => SetValue(AltoMaximoContenidoProperty, value);
    }

    public bool MostrarLeyendaObligatorios
    {
        get => (bool)GetValue(MostrarLeyendaObligatoriosProperty);
        set => SetValue(MostrarLeyendaObligatoriosProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // Al abrirse el diálogo el foco va al primer campo editable, para poder
        // escribir de inmediato sin tocar el ratón.
        Loaded += (_, _) => Dispatcher.BeginInvoke(new Action(() => MoveFocus(
            new TraversalRequest(FocusNavigationDirection.First))),
            System.Windows.Threading.DispatcherPriority.Input);
    }
}
