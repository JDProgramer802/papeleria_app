using System.Reflection;
using System.Windows;
using System.Windows.Media.Animation;

namespace Papeleria.App.Views;

/// <summary>
/// Pantalla de carga. Muestra el avance real de la preparación del sistema
/// (migraciones, datos maestros y configuración) en lugar de un temporizador ficticio.
/// </summary>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        TextoVersion.Text = $"Versión {version?.ToString(3) ?? "1.0.0"}";
    }

    /// <summary>
    /// Recorta el contenido con las esquinas redondeadas de la ventana. Sin esto, los
    /// círculos decorativos del fondo se salen por las cuatro esquinas.
    /// </summary>
    private void AjustarRecorte(object remitente, SizeChangedEventArgs argumentos)
    {
        if (remitente is System.Windows.Controls.Grid marco)
        {
            marco.Clip = new System.Windows.Media.RectangleGeometry(
                new Rect(0, 0, marco.ActualWidth, marco.ActualHeight), 13, 13);
        }
    }

    /// <summary>Actualiza el mensaje y anima la barra hasta el porcentaje indicado.</summary>
    public void ActualizarProgreso(string mensaje, int porcentaje)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => ActualizarProgreso(mensaje, porcentaje));
            return;
        }

        TextoEstado.Text = mensaje;

        // La transición animada evita saltos bruscos cuando un paso avanza mucho de golpe.
        var animacion = new DoubleAnimation
        {
            To = Math.Clamp(porcentaje, 0, 100),
            Duration = TimeSpan.FromMilliseconds(320),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        BarraProgreso.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, animacion);
    }
}
