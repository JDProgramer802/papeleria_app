using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;

namespace Papeleria.App.Controls;

/// <summary>
/// Gráfico comparativo de dos series mensuales dibujado con primitivas de WPF. Se hizo
/// a medida para no arrastrar dependencias nativas (SkiaSharp/OpenGL) en un ejecutable
/// que debe ser autónomo y funcionar sin conexión, y para que los colores sigan el tema.
/// La serie de barras da el volumen de cada mes y la de línea, dibujada encima, deja ver
/// la tendencia sin tener que comparar alturas de barras vecinas.
/// </summary>
public class GraficoComparativo : Decorator
{
    private const double MargenIzquierdo = 62;
    private const double MargenInferior = 26;
    private const double MargenSuperior = 12;
    private const double MargenDerecho = 10;
    private const int LineasRejilla = 4;
    private const double RadioPunto = 3.5;
    private const int DuracionAnimacion = 520;

    /// <summary>
    /// El lienzo es propio del control. Antes se obtenía de una plantilla por nombre y
    /// la búsqueda devolvía siempre <c>null</c>, de modo que el gráfico jamás se pintaba.
    /// </summary>
    private readonly Canvas _lienzo = new() { ClipToBounds = true, Background = Brushes.Transparent };

    public GraficoComparativo()
    {
        Child = _lienzo;
        SizeChanged += (_, _) => Redibujar();
    }

    // ── Propiedades de dependencia ──────────────────────────────────────────

    public static readonly DependencyProperty SerieBarrasProperty = DependencyProperty.Register(
        nameof(SerieBarras), typeof(IEnumerable), typeof(GraficoComparativo),
        new PropertyMetadata(null, AlCambiarDatos));

    public static readonly DependencyProperty SerieLineaProperty = DependencyProperty.Register(
        nameof(SerieLinea), typeof(IEnumerable), typeof(GraficoComparativo),
        new PropertyMetadata(null, AlCambiarDatos));

    public static readonly DependencyProperty NombreSerieBarrasProperty = DependencyProperty.Register(
        nameof(NombreSerieBarras), typeof(string), typeof(GraficoComparativo),
        new PropertyMetadata("Serie", AlCambiarDatos));

    public static readonly DependencyProperty NombreSerieLineaProperty = DependencyProperty.Register(
        nameof(NombreSerieLinea), typeof(string), typeof(GraficoComparativo),
        new PropertyMetadata("Serie", AlCambiarDatos));

    public static readonly DependencyProperty ColorBarrasProperty = DependencyProperty.Register(
        nameof(ColorBarras), typeof(Brush), typeof(GraficoComparativo),
        new PropertyMetadata(CrearBrocha("#FB8C00"), AlCambiarDatos));

    public static readonly DependencyProperty ColorLineaProperty = DependencyProperty.Register(
        nameof(ColorLinea), typeof(Brush), typeof(GraficoComparativo),
        new PropertyMetadata(CrearBrocha("#1E88E5"), AlCambiarDatos));

    /// <summary>
    /// Permite apagar la animación de entrada. El banco de pruebas la desactiva para
    /// capturar el gráfico ya dibujado en vez de a medio crecer.
    /// </summary>
    public static readonly DependencyProperty AnimarProperty = DependencyProperty.Register(
        nameof(Animar), typeof(bool), typeof(GraficoComparativo), new PropertyMetadata(true));

    /// <summary>Serie representada con barras; da el volumen de cada periodo.</summary>
    public IEnumerable? SerieBarras
    {
        get => (IEnumerable?)GetValue(SerieBarrasProperty);
        set => SetValue(SerieBarrasProperty, value);
    }

    /// <summary>Serie representada con una línea sobre las barras; muestra la tendencia.</summary>
    public IEnumerable? SerieLinea
    {
        get => (IEnumerable?)GetValue(SerieLineaProperty);
        set => SetValue(SerieLineaProperty, value);
    }

    public string NombreSerieBarras
    {
        get => (string)GetValue(NombreSerieBarrasProperty);
        set => SetValue(NombreSerieBarrasProperty, value);
    }

    public string NombreSerieLinea
    {
        get => (string)GetValue(NombreSerieLineaProperty);
        set => SetValue(NombreSerieLineaProperty, value);
    }

    public Brush ColorBarras
    {
        get => (Brush)GetValue(ColorBarrasProperty);
        set => SetValue(ColorBarrasProperty, value);
    }

    public Brush ColorLinea
    {
        get => (Brush)GetValue(ColorLineaProperty);
        set => SetValue(ColorLineaProperty, value);
    }

    public bool Animar
    {
        get => (bool)GetValue(AnimarProperty);
        set => SetValue(AnimarProperty, value);
    }

    /// <summary>Número de figuras dibujadas; lo usan las pruebas para detectar un lienzo vacío.</summary>
    public int ElementosDibujados => _lienzo.Children.Count;

    private static void AlCambiarDatos(DependencyObject destino, DependencyPropertyChangedEventArgs argumentos)
    {
        if (destino is not GraficoComparativo grafico)
        {
            return;
        }

        // El modelo de vista reutiliza la misma colección y sólo cambia su contenido:
        // sin escuchar sus cambios, el gráfico se quedaría vacío tras cada recarga.
        if (argumentos.Property == SerieBarrasProperty || argumentos.Property == SerieLineaProperty)
        {
            grafico.CambiarSuscripcion(argumentos.OldValue, argumentos.NewValue);
        }

        grafico.Redibujar();
    }

    private void CambiarSuscripcion(object? anterior, object? nueva)
    {
        if (anterior is INotifyCollectionChanged viejaColeccion)
        {
            viejaColeccion.CollectionChanged -= AlCambiarLaColeccion;
        }

        if (nueva is INotifyCollectionChanged nuevaColeccion)
        {
            nuevaColeccion.CollectionChanged += AlCambiarLaColeccion;
        }
    }

    private void AlCambiarLaColeccion(object? remitente, NotifyCollectionChangedEventArgs argumentos) =>
        Dispatcher.BeginInvoke(new Action(Redibujar), DispatcherPriority.Render);

    private static SolidColorBrush CrearBrocha(string hexadecimal)
    {
        var brocha = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexadecimal));
        brocha.Freeze();
        return brocha;
    }

    // ── Dibujo ──────────────────────────────────────────────────────────────

    private void Redibujar()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        _lienzo.Children.Clear();

        var barras = Convertir(SerieBarras);
        var linea = Convertir(SerieLinea);

        // Las series siempre traen los doce meses, con cero donde no hubo movimiento;
        // por eso no basta con mirar si vienen vacías para saber que no hay nada que enseñar.
        var hayDatos = barras.Any(p => p.Valor > 0) || linea.Any(p => p.Valor > 0);

        if (!hayDatos)
        {
            DibujarMensajeVacio();
            return;
        }

        var anchoUtil = Math.Max(ActualWidth - MargenIzquierdo - MargenDerecho, 10);
        var altoUtil = Math.Max(ActualHeight - MargenSuperior - MargenInferior, 10);

        var maximo = CalcularMaximo(barras, linea);
        var pincelTexto = (Brush?)TryFindResource("MaterialDesignBodyLight") ?? Brushes.Gray;
        var pincelRejilla = (Brush?)TryFindResource("MaterialDesignDivider") ?? Brushes.LightGray;

        DibujarRejillaYEjeY(anchoUtil, altoUtil, maximo, pincelTexto, pincelRejilla);

        var cantidadPeriodos = Math.Max(barras.Count, linea.Count);
        var anchoPeriodo = anchoUtil / cantidadPeriodos;

        // La barra ocupa el 62 % de su franja; el resto es aire entre meses.
        var anchoBarra = anchoPeriodo * 0.62;

        for (var indice = 0; indice < cantidadPeriodos; indice++)
        {
            var centro = MargenIzquierdo + indice * anchoPeriodo + anchoPeriodo / 2;

            if (indice < barras.Count)
            {
                DibujarBarra(barras[indice], centro - anchoBarra / 2, anchoBarra, altoUtil, maximo);
            }

            var etiqueta = indice < barras.Count ? barras[indice].Etiqueta : linea[indice].Etiqueta;
            DibujarEtiquetaEjeX(etiqueta, centro - anchoPeriodo / 2, anchoPeriodo, altoUtil, pincelTexto);
        }

        // La línea va después de las barras para quedar por encima de ellas.
        DibujarLinea(linea, anchoUtil, altoUtil, maximo, cantidadPeriodos);
    }

    private void DibujarMensajeVacio()
    {
        var texto = new TextBlock
        {
            Text = "Aún no hay ventas ni compras registradas",
            Foreground = (Brush?)TryFindResource("MaterialDesignBodyLight") ?? Brushes.Gray,
            FontSize = 12.5
        };

        texto.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        Canvas.SetLeft(texto, (ActualWidth - texto.DesiredSize.Width) / 2);
        Canvas.SetTop(texto, (ActualHeight - texto.DesiredSize.Height) / 2);

        _lienzo.Children.Add(texto);
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

            _lienzo.Children.Add(linea);

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

    private void DibujarBarra(PuntoSerie punto, double x, double ancho, double altoUtil, decimal maximo)
    {
        if (ancho <= 0)
        {
            return;
        }

        var proporcion = maximo == 0 ? 0 : (double)(punto.Valor / maximo);
        var altoBarra = Math.Max(altoUtil * proporcion, punto.Valor > 0 ? 2 : 0);
        var baseEje = MargenSuperior + altoUtil;

        var barra = new Rectangle
        {
            Width = ancho,
            Height = Animar ? 0 : altoBarra,
            RadiusX = 3,
            RadiusY = 3,
            Fill = ColorBarras,
            ToolTip = DescribirPunto(NombreSerieBarras, punto),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        // El resaltado al pasar el ratón da retroalimentación sin recalcular el gráfico.
        barra.MouseEnter += (_, _) => barra.Opacity = 0.75;
        barra.MouseLeave += (_, _) => barra.Opacity = 1;

        Canvas.SetLeft(barra, x);
        Canvas.SetTop(barra, Animar ? baseEje : baseEje - altoBarra);

        _lienzo.Children.Add(barra);

        if (!Animar)
        {
            return;
        }

        // Crecimiento animado desde la base del eje.
        barra.BeginAnimation(FrameworkElement.HeightProperty, new DoubleAnimation
        {
            From = 0,
            To = altoBarra,
            Duration = TimeSpan.FromMilliseconds(DuracionAnimacion),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });

        barra.BeginAnimation(Canvas.TopProperty, new DoubleAnimation
        {
            From = baseEje,
            To = baseEje - altoBarra,
            Duration = TimeSpan.FromMilliseconds(DuracionAnimacion),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    /// <summary>Traza la serie de tendencia y marca cada periodo con un punto.</summary>
    private void DibujarLinea(
        List<PuntoSerie> puntos, double anchoUtil, double altoUtil, decimal maximo, int cantidadPeriodos)
    {
        if (puntos.Count == 0)
        {
            return;
        }

        var anchoPeriodo = anchoUtil / cantidadPeriodos;
        var coordenadas = new PointCollection();

        for (var indice = 0; indice < puntos.Count; indice++)
        {
            var proporcion = maximo == 0 ? 0 : (double)(puntos[indice].Valor / maximo);

            coordenadas.Add(new Point(
                MargenIzquierdo + indice * anchoPeriodo + anchoPeriodo / 2,
                MargenSuperior + altoUtil - altoUtil * proporcion));
        }

        var trazo = new Polyline
        {
            Points = coordenadas,
            Stroke = ColorLinea,
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false
        };

        _lienzo.Children.Add(trazo);

        // El borde claro despega el punto de la barra que tiene debajo.
        var bordePunto = (Brush?)TryFindResource("MaterialDesignPaper") ?? Brushes.White;

        for (var indice = 0; indice < puntos.Count; indice++)
        {
            var marca = new Ellipse
            {
                Width = RadioPunto * 2,
                Height = RadioPunto * 2,
                Fill = ColorLinea,
                Stroke = bordePunto,
                StrokeThickness = 1.5,
                ToolTip = DescribirPunto(NombreSerieLinea, puntos[indice]),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            Canvas.SetLeft(marca, coordenadas[indice].X - RadioPunto);
            Canvas.SetTop(marca, coordenadas[indice].Y - RadioPunto);

            _lienzo.Children.Add(marca);
        }

        if (!Animar)
        {
            return;
        }

        // La línea aparece con las barras ya crecidas, para que no cruce el aire vacío.
        var aparicion = new DoubleAnimation
        {
            From = 0,
            To = 1,
            BeginTime = TimeSpan.FromMilliseconds(DuracionAnimacion / 2),
            Duration = TimeSpan.FromMilliseconds(DuracionAnimacion / 2)
        };

        trazo.BeginAnimation(OpacityProperty, aparicion);

        foreach (var marca in _lienzo.Children.OfType<Ellipse>())
        {
            marca.BeginAnimation(OpacityProperty, aparicion);
        }
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

        _lienzo.Children.Add(etiqueta);
    }

    private static string DescribirPunto(string nombreSerie, PuntoSerie punto) =>
        $"{nombreSerie} · {punto.Etiqueta} {punto.Periodo:yyyy}\n{Formatos.Moneda(punto.Valor)}";

    private static List<PuntoSerie> Convertir(IEnumerable? origen) =>
        origen?.Cast<object>().OfType<PuntoSerie>().ToList() ?? new List<PuntoSerie>();

    private static decimal CalcularMaximo(List<PuntoSerie> barras, List<PuntoSerie> linea)
    {
        var maximo = Math.Max(
            barras.Count == 0 ? 0 : barras.Max(p => p.Valor),
            linea.Count == 0 ? 0 : linea.Max(p => p.Valor));

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
