using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;

namespace Papeleria.App.Controls;

/// <summary>
/// Gráfico de barras dibujado con primitivas de WPF. Se implementó a medida para no
/// arrastrar dependencias nativas (SkiaSharp/OpenGL) en un ejecutable que debe ser
/// autónomo y funcionar sin conexión, y para que los colores sigan el tema activo.
/// Admite una o dos series comparadas, con ejes, rejilla, etiquetas y animación de entrada.
/// </summary>
public class GraficoBarras : Control
{
    private const double MargenIzquierdo = 62;
    private const double MargenInferior = 26;
    private const double MargenSuperior = 12;
    private const double MargenDerecho = 10;
    private const int LineasRejilla = 4;

    private Canvas? _lienzo;

    static GraficoBarras()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(GraficoBarras), new FrameworkPropertyMetadata(typeof(GraficoBarras)));
    }

    public GraficoBarras()
    {
        // El estilo por defecto se define en código para que el control funcione
        // sin necesidad de un Themes/Generic.xaml.
        Template = ConstruirPlantilla();
        SizeChanged += (_, _) => Redibujar();
        Loaded += (_, _) => Redibujar();
    }

    // ── Propiedades de dependencia ──────────────────────────────────────────

    public static readonly DependencyProperty SerieAProperty = DependencyProperty.Register(
        nameof(SerieA), typeof(IEnumerable), typeof(GraficoBarras),
        new PropertyMetadata(null, AlCambiarDatos));

    public static readonly DependencyProperty SerieBProperty = DependencyProperty.Register(
        nameof(SerieB), typeof(IEnumerable), typeof(GraficoBarras),
        new PropertyMetadata(null, AlCambiarDatos));

    public static readonly DependencyProperty NombreSerieAProperty = DependencyProperty.Register(
        nameof(NombreSerieA), typeof(string), typeof(GraficoBarras),
        new PropertyMetadata("Serie A", AlCambiarDatos));

    public static readonly DependencyProperty NombreSerieBProperty = DependencyProperty.Register(
        nameof(NombreSerieB), typeof(string), typeof(GraficoBarras),
        new PropertyMetadata("Serie B", AlCambiarDatos));

    public static readonly DependencyProperty ColorSerieAProperty = DependencyProperty.Register(
        nameof(ColorSerieA), typeof(Brush), typeof(GraficoBarras),
        new PropertyMetadata(CrearBrocha("#1E88E5"), AlCambiarDatos));

    public static readonly DependencyProperty ColorSerieBProperty = DependencyProperty.Register(
        nameof(ColorSerieB), typeof(Brush), typeof(GraficoBarras),
        new PropertyMetadata(CrearBrocha("#FB8C00"), AlCambiarDatos));

    /// <summary>Serie principal de puntos a graficar.</summary>
    public IEnumerable? SerieA
    {
        get => (IEnumerable?)GetValue(SerieAProperty);
        set => SetValue(SerieAProperty, value);
    }

    /// <summary>Serie opcional de comparación; si es nula se dibuja una sola barra por periodo.</summary>
    public IEnumerable? SerieB
    {
        get => (IEnumerable?)GetValue(SerieBProperty);
        set => SetValue(SerieBProperty, value);
    }

    public string NombreSerieA
    {
        get => (string)GetValue(NombreSerieAProperty);
        set => SetValue(NombreSerieAProperty, value);
    }

    public string NombreSerieB
    {
        get => (string)GetValue(NombreSerieBProperty);
        set => SetValue(NombreSerieBProperty, value);
    }

    public Brush ColorSerieA
    {
        get => (Brush)GetValue(ColorSerieAProperty);
        set => SetValue(ColorSerieAProperty, value);
    }

    public Brush ColorSerieB
    {
        get => (Brush)GetValue(ColorSerieBProperty);
        set => SetValue(ColorSerieBProperty, value);
    }

    private static void AlCambiarDatos(DependencyObject destino, DependencyPropertyChangedEventArgs argumentos) =>
        (destino as GraficoBarras)?.Redibujar();

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _lienzo = GetTemplateChild("PARTE_Lienzo") as Canvas;
        Redibujar();
    }

    private static ControlTemplate ConstruirPlantilla()
    {
        var lienzo = new FrameworkElementFactory(typeof(Canvas));
        lienzo.SetValue(NameProperty, "PARTE_Lienzo");
        lienzo.SetValue(Panel.BackgroundProperty, Brushes.Transparent);
        lienzo.SetValue(ClipToBoundsProperty, true);

        return new ControlTemplate(typeof(GraficoBarras)) { VisualTree = lienzo };
    }

    private static SolidColorBrush CrearBrocha(string hexadecimal)
    {
        var brocha = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexadecimal));
        brocha.Freeze();
        return brocha;
    }

    // ── Dibujo ──────────────────────────────────────────────────────────────

    private void Redibujar()
    {
        if (_lienzo is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        _lienzo.Children.Clear();

        var serieA = Convertir(SerieA);
        var serieB = Convertir(SerieB);

        if (serieA.Count == 0 && serieB.Count == 0)
        {
            DibujarMensajeVacio();
            return;
        }

        var ancho = ActualWidth;
        var alto = ActualHeight;
        var anchoUtil = Math.Max(ancho - MargenIzquierdo - MargenDerecho, 10);
        var altoUtil = Math.Max(alto - MargenSuperior - MargenInferior, 10);

        var maximo = CalcularMaximo(serieA, serieB);
        var pincelTexto = (Brush?)TryFindResource("MaterialDesignBodyLight") ?? Brushes.Gray;
        var pincelRejilla = (Brush?)TryFindResource("MaterialDesignDivider") ?? Brushes.LightGray;

        DibujarRejillaYEjeY(anchoUtil, altoUtil, maximo, pincelTexto, pincelRejilla);

        var cantidadPeriodos = Math.Max(serieA.Count, serieB.Count);
        var anchoPeriodo = anchoUtil / cantidadPeriodos;
        var haySegundaSerie = serieB.Count > 0;

        // Se reserva un 26 % del espacio del periodo como separación entre grupos.
        var anchoGrupo = anchoPeriodo * 0.74;
        var anchoBarra = haySegundaSerie ? anchoGrupo / 2 - 2 : anchoGrupo;

        for (var indice = 0; indice < cantidadPeriodos; indice++)
        {
            var xBase = MargenIzquierdo + indice * anchoPeriodo + (anchoPeriodo - anchoGrupo) / 2;

            if (indice < serieA.Count)
            {
                DibujarBarra(serieA[indice], xBase, anchoBarra, altoUtil, maximo, ColorSerieA, NombreSerieA);
            }

            if (haySegundaSerie && indice < serieB.Count)
            {
                DibujarBarra(serieB[indice], xBase + anchoBarra + 4, anchoBarra, altoUtil, maximo,
                    ColorSerieB, NombreSerieB);
            }

            var etiqueta = indice < serieA.Count ? serieA[indice].Etiqueta : serieB[indice].Etiqueta;
            DibujarEtiquetaEjeX(etiqueta, xBase, anchoGrupo, altoUtil, pincelTexto);
        }
    }

    private void DibujarMensajeVacio()
    {
        var texto = new TextBlock
        {
            Text = "Sin datos para mostrar",
            Foreground = (Brush?)TryFindResource("MaterialDesignBodyLight") ?? Brushes.Gray,
            FontSize = 12.5
        };

        texto.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        Canvas.SetLeft(texto, (ActualWidth - texto.DesiredSize.Width) / 2);
        Canvas.SetTop(texto, (ActualHeight - texto.DesiredSize.Height) / 2);

        _lienzo!.Children.Add(texto);
    }

    private void DibujarRejillaYEjeY(
        double anchoUtil, double altoUtil, decimal maximo, Brush pincelTexto, Brush pincelRejilla)
    {
        for (var i = 0; i <= LineasRejilla; i++)
        {
            var proporcion = (double)i / LineasRejilla;
            var y = MargenSuperior + altoUtil - altoUtil * proporcion;

            var linea = new Line
            {
                X1 = MargenIzquierdo,
                X2 = MargenIzquierdo + anchoUtil,
                Y1 = y,
                Y2 = y,
                Stroke = pincelRejilla,
                StrokeThickness = i == 0 ? 1 : 0.6,
                SnapsToDevicePixels = true
            };

            if (i > 0)
            {
                linea.StrokeDashArray = new DoubleCollection { 3, 4 };
            }

            _lienzo!.Children.Add(linea);

            var valor = maximo * (decimal)proporcion;

            var etiqueta = new TextBlock
            {
                Text = AbreviarImporte(valor),
                FontSize = 10,
                Foreground = pincelTexto
            };

            etiqueta.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            Canvas.SetLeft(etiqueta, MargenIzquierdo - etiqueta.DesiredSize.Width - 8);
            Canvas.SetTop(etiqueta, y - etiqueta.DesiredSize.Height / 2);

            _lienzo.Children.Add(etiqueta);
        }
    }

    private void DibujarBarra(
        PuntoSerie punto, double x, double ancho, double altoUtil, decimal maximo,
        Brush color, string nombreSerie)
    {
        if (ancho <= 0)
        {
            return;
        }

        var proporcion = maximo == 0 ? 0 : (double)(punto.Valor / maximo);
        var altoBarra = Math.Max(altoUtil * proporcion, punto.Valor > 0 ? 2 : 0);

        var barra = new Rectangle
        {
            Width = ancho,
            Height = 0,
            RadiusX = 3,
            RadiusY = 3,
            Fill = color,
            ToolTip = $"{nombreSerie} · {punto.Etiqueta} {punto.Periodo:yyyy}\n{Formatos.Moneda(punto.Valor)}",
            Cursor = System.Windows.Input.Cursors.Hand
        };

        // El resaltado al pasar el ratón da retroalimentación sin recalcular el gráfico.
        barra.MouseEnter += (_, _) => barra.Opacity = 0.75;
        barra.MouseLeave += (_, _) => barra.Opacity = 1;

        Canvas.SetLeft(barra, x);
        Canvas.SetTop(barra, MargenSuperior + altoUtil);

        _lienzo!.Children.Add(barra);

        // Crecimiento animado desde la base del eje.
        var animacionAlto = new DoubleAnimation
        {
            From = 0,
            To = altoBarra,
            Duration = TimeSpan.FromMilliseconds(520),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var animacionPosicion = new DoubleAnimation
        {
            From = MargenSuperior + altoUtil,
            To = MargenSuperior + altoUtil - altoBarra,
            Duration = TimeSpan.FromMilliseconds(520),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        barra.BeginAnimation(HeightProperty, animacionAlto);
        barra.BeginAnimation(Canvas.TopProperty, animacionPosicion);
    }

    private void DibujarEtiquetaEjeX(
        string texto, double x, double anchoGrupo, double altoUtil, Brush pincelTexto)
    {
        var etiqueta = new TextBlock
        {
            Text = texto,
            FontSize = 10,
            Foreground = pincelTexto,
            TextAlignment = TextAlignment.Center,
            Width = anchoGrupo
        };

        Canvas.SetLeft(etiqueta, x);
        Canvas.SetTop(etiqueta, MargenSuperior + altoUtil + 6);

        _lienzo!.Children.Add(etiqueta);
    }

    private static List<PuntoSerie> Convertir(IEnumerable? origen) =>
        origen?.Cast<object>().OfType<PuntoSerie>().ToList() ?? new List<PuntoSerie>();

    private static decimal CalcularMaximo(List<PuntoSerie> serieA, List<PuntoSerie> serieB)
    {
        var maximo = Math.Max(
            serieA.Count == 0 ? 0 : serieA.Max(p => p.Valor),
            serieB.Count == 0 ? 0 : serieB.Max(p => p.Valor));

        if (maximo <= 0)
        {
            return 1;
        }

        // Se redondea hacia arriba a una escala «bonita» para que el eje sea legible.
        var magnitud = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)maximo)));
        return Math.Ceiling(maximo / magnitud) * magnitud;
    }

    /// <summary>Abrevia importes grandes en el eje: 1,2 M en vez de 1.200.000.</summary>
    private static string AbreviarImporte(decimal valor) => valor switch
    {
        >= 1_000_000_000 => (valor / 1_000_000_000).ToString("0.#", Formatos.Cultura) + " MM",
        >= 1_000_000 => (valor / 1_000_000).ToString("0.#", Formatos.Cultura) + " M",
        >= 1_000 => (valor / 1_000).ToString("0.#", Formatos.Cultura) + " k",
        _ => valor.ToString("0", CultureInfo.InvariantCulture)
    };
}
