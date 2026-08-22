using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Papeleria.App.Controls;

/// <summary>
/// Tarjeta de indicador del panel principal. Antepone la cifra a la etiqueta —que es
/// lo que se busca de un vistazo— y deja el icono como acento de color a la derecha.
/// Sustituye a las nueve copias del mismo bloque que había en la vista.
/// La plantilla vive en <c>Resources/Controles.xaml</c>.
/// </summary>
public class TarjetaMetrica : Control
{
    static TarjetaMetrica()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TarjetaMetrica), new FrameworkPropertyMetadata(typeof(TarjetaMetrica)));
    }

    public static readonly DependencyProperty EtiquetaProperty = DependencyProperty.Register(
        nameof(Etiqueta), typeof(string), typeof(TarjetaMetrica), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValorProperty = DependencyProperty.Register(
        nameof(Valor), typeof(string), typeof(TarjetaMetrica), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty NotaProperty = DependencyProperty.Register(
        nameof(Nota), typeof(string), typeof(TarjetaMetrica), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconoProperty = DependencyProperty.Register(
        nameof(Icono), typeof(PackIconKind), typeof(TarjetaMetrica),
        new PropertyMetadata(PackIconKind.ChartBoxOutline));

    public static readonly DependencyProperty ColorIconoProperty = DependencyProperty.Register(
        nameof(ColorIcono), typeof(Brush), typeof(TarjetaMetrica), new PropertyMetadata(null));

    public static readonly DependencyProperty FondoIconoProperty = DependencyProperty.Register(
        nameof(FondoIcono), typeof(Brush), typeof(TarjetaMetrica), new PropertyMetadata(null));

    /// <summary>Contenido opcional bajo la cifra: se usa para la variación mensual.</summary>
    public static readonly DependencyProperty PieProperty = DependencyProperty.Register(
        nameof(Pie), typeof(object), typeof(TarjetaMetrica), new PropertyMetadata(null));

    /// <summary>Resalta la tarjeta cuando el dato exige actuar (agotados, bajo mínimo).</summary>
    public static readonly DependencyProperty EsAlertaProperty = DependencyProperty.Register(
        nameof(EsAlerta), typeof(bool), typeof(TarjetaMetrica), new PropertyMetadata(false));

    public string Etiqueta
    {
        get => (string)GetValue(EtiquetaProperty);
        set => SetValue(EtiquetaProperty, value);
    }

    public string Valor
    {
        get => (string)GetValue(ValorProperty);
        set => SetValue(ValorProperty, value);
    }

    public string Nota
    {
        get => (string)GetValue(NotaProperty);
        set => SetValue(NotaProperty, value);
    }

    public PackIconKind Icono
    {
        get => (PackIconKind)GetValue(IconoProperty);
        set => SetValue(IconoProperty, value);
    }

    public Brush? ColorIcono
    {
        get => (Brush?)GetValue(ColorIconoProperty);
        set => SetValue(ColorIconoProperty, value);
    }

    public Brush? FondoIcono
    {
        get => (Brush?)GetValue(FondoIconoProperty);
        set => SetValue(FondoIconoProperty, value);
    }

    public object? Pie
    {
        get => GetValue(PieProperty);
        set => SetValue(PieProperty, value);
    }

    public bool EsAlerta
    {
        get => (bool)GetValue(EsAlertaProperty);
        set => SetValue(EsAlertaProperty, value);
    }
}
