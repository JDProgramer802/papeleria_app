using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;

namespace Papeleria.App.Converters;

/// <summary>Muestra u oculta según un booleano. Con el parámetro «Invertir» hace lo contrario.</summary>
public class BooleanoAVisibilidad : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value is true;

        if (EsInvertido(parameter))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value is Visibility.Visible;
        return EsInvertido(parameter) ? !visible : visible;
    }

    internal static bool EsInvertido(object? parameter) =>
        parameter is string texto &&
        (texto.Equals("Invertir", StringComparison.OrdinalIgnoreCase) ||
         texto.Equals("Invert", StringComparison.OrdinalIgnoreCase));
}

/// <summary>Niega un valor booleano.</summary>
public class InvertirBooleano : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;
}

/// <summary>Visible cuando el valor no es nulo (o al revés con «Invertir»).</summary>
public class NuloAVisibilidad : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hayValor = value is not null;

        if (BooleanoAVisibilidad.EsInvertido(parameter))
        {
            hayValor = !hayValor;
        }

        return hayValor ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Visible cuando la cadena tiene contenido.</summary>
public class TextoAVisibilidad : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hayTexto = !string.IsNullOrWhiteSpace(value as string);

        if (BooleanoAVisibilidad.EsInvertido(parameter))
        {
            hayTexto = !hayTexto;
        }

        return hayTexto ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Visible cuando la colección tiene elementos.</summary>
public class ColeccionVaciaAVisibilidad : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var cantidad = value switch
        {
            null => 0,
            int numero => numero,
            System.Collections.ICollection coleccion => coleccion.Count,
            System.Collections.IEnumerable enumerable => enumerable.Cast<object>().Count(),
            _ => 0
        };

        var vacia = cantidad == 0;

        // Sin parámetro se muestra el mensaje de «sin datos»; con «Invertir», la lista.
        return (BooleanoAVisibilidad.EsInvertido(parameter) ? !vacia : vacia)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Formatea importes en pesos colombianos.</summary>
public class MonedaConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var decimales = parameter is string texto && int.TryParse(texto, out var cantidad) ? cantidad : 0;

        return Formatos.Moneda(System.Convert.ToDecimal(value, CultureInfo.InvariantCulture), decimales);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string texto)
        {
            return Binding.DoNothing;
        }

        var limpio = new string(texto.Where(c => char.IsDigit(c) || c is ',' or '.' or '-').ToArray());

        return decimal.TryParse(limpio, NumberStyles.Any, Formatos.Cultura, out var resultado)
            ? resultado
            : Binding.DoNothing;
    }
}

/// <summary>Formatea cantidades ocultando los decimales cuando son enteras.</summary>
public class CantidadConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null
            ? string.Empty
            : Formatos.Cantidad(System.Convert.ToDecimal(value, CultureInfo.InvariantCulture));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string texto &&
        decimal.TryParse(texto, NumberStyles.Any, Formatos.Cultura, out var resultado)
            ? resultado
            : Binding.DoNothing;
}

/// <summary>Añade el símbolo de porcentaje.</summary>
public class PorcentajeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var decimales = parameter is string texto && int.TryParse(texto, out var cantidad) ? cantidad : 1;

        return Formatos.Porcentaje(System.Convert.ToDecimal(value, CultureInfo.InvariantCulture), decimales);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Formatea fechas; con el parámetro «Hora» incluye la hora.</summary>
public class FechaConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime fecha)
        {
            return "—";
        }

        return parameter switch
        {
            "Hora" => Formatos.FechaHora(fecha),
            "SoloHora" => Formatos.Hora(fecha),
            "Larga" => Formatos.FechaLarga(fecha),
            _ => Formatos.Fecha(fecha)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Color del semáforo de existencias.</summary>
public class EstadoStockABrocha : IValueConverter
{
    private static readonly SolidColorBrush Agotado = Congelar("#E53935");
    private static readonly SolidColorBrush Bajo = Congelar("#FB8C00");
    private static readonly SolidColorBrush Normal = Congelar("#43A047");
    private static readonly SolidColorBrush Exceso = Congelar("#1E88E5");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            EstadoStock.Agotado => Agotado,
            EstadoStock.Bajo => Bajo,
            EstadoStock.Exceso => Exceso,
            _ => Normal
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    internal static SolidColorBrush Congelar(string hexadecimal)
    {
        var brocha = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexadecimal));
        brocha.Freeze();
        return brocha;
    }
}

/// <summary>Verde para variaciones positivas, rojo para negativas y gris cuando no hay cambio.</summary>
public class VariacionABrocha : IValueConverter
{
    private static readonly SolidColorBrush Positiva = EstadoStockABrocha.Congelar("#2E7D32");
    private static readonly SolidColorBrush Negativa = EstadoStockABrocha.Congelar("#C62828");
    private static readonly SolidColorBrush Neutra = EstadoStockABrocha.Congelar("#757575");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return Neutra;
        }

        var numero = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);

        // En compras y egresos, que suban no es necesariamente bueno: se puede invertir.
        if (BooleanoAVisibilidad.EsInvertido(parameter))
        {
            numero = -numero;
        }

        return numero switch
        {
            > 0 => Positiva,
            < 0 => Negativa,
            _ => Neutra
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Flecha ascendente o descendente según el signo de la variación.</summary>
public class VariacionAIcono : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return MaterialDesignThemes.Wpf.PackIconKind.Minus;
        }

        var numero = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);

        return numero switch
        {
            > 0 => MaterialDesignThemes.Wpf.PackIconKind.TrendingUp,
            < 0 => MaterialDesignThemes.Wpf.PackIconKind.TrendingDown,
            _ => MaterialDesignThemes.Wpf.PackIconKind.TrendingNeutral
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Muestra la variación con su signo delante.</summary>
public class VariacionATexto : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return "—";
        }

        var numero = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        var signo = numero > 0 ? "+" : string.Empty;

        return $"{signo}{Formatos.Numero(numero, 1)} %";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>
/// Carga una imagen desde disco sin bloquear el archivo, para que el producto pueda
/// reemplazarla mientras la aplicación está abierta.
/// </summary>
public class RutaAImagen : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string ruta || string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
        {
            return null;
        }

        try
        {
            var imagen = new BitmapImage();
            imagen.BeginInit();
            imagen.CacheOption = BitmapCacheOption.OnLoad;
            imagen.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            imagen.UriSource = new Uri(ruta, UriKind.Absolute);
            imagen.DecodePixelWidth = 320;
            imagen.EndInit();
            imagen.Freeze();

            return imagen;
        }
        catch (Exception)
        {
            // Una imagen corrupta no debe tumbar la grilla: simplemente no se muestra.
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Convierte los bytes PNG de un código de barras en una imagen mostrable.</summary>
public class BytesAImagen : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] datos || datos.Length == 0)
        {
            return null;
        }

        try
        {
            using var flujo = new MemoryStream(datos);

            var imagen = new BitmapImage();
            imagen.BeginInit();
            imagen.CacheOption = BitmapCacheOption.OnLoad;
            imagen.StreamSource = flujo;
            imagen.EndInit();
            imagen.Freeze();

            return imagen;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Compara el valor con el parámetro; útil para pestañas y selectores.</summary>
public class IgualAParametro : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString()?.Equals(parameter?.ToString(), StringComparison.OrdinalIgnoreCase) == true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is not null ? parameter : Binding.DoNothing;
}

/// <summary>Devuelve el texto del atributo Display de un enumerado.</summary>
public class EnumADescripcion : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Enum enumerado ? enumerado.Descripcion() : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Visible cuando el número es mayor que cero.</summary>
/// <summary>
/// Indica si un número es mayor que cero. Se usa para encender avisos visuales
/// cuando un contador deja de estar a cero (productos agotados, bajo mínimo).
/// </summary>
/// <summary>
/// Pasa el índice de una lista, que empieza en cero, al número que ve el usuario.
/// Se usa en los pasos numerados del manual.
/// </summary>
/// <summary>
/// Convierte un porcentaje en una proporción de rejilla. Sirve para dibujar barras
/// de avance con dos bordes, que se ven siempre, sin depender de una animación.
/// </summary>
public class PorcentajeAProporcion : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var porcentaje = value is null ? 0 : System.Convert.ToDouble(value, CultureInfo.InvariantCulture);

        return new System.Windows.GridLength(Math.Max(porcentaje, 0), System.Windows.GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class IndiceANumero : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int indice ? (indice + 1).ToString(CultureInfo.InvariantCulture) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class MayorQueCeroABooleano : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var numero = value is null ? 0 : System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        var positivo = numero > 0;

        return BooleanoAVisibilidad.EsInvertido(parameter) ? !positivo : positivo;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class MayorQueCeroAVisibilidad : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var numero = value is null ? 0 : System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        var positivo = numero > 0;

        if (BooleanoAVisibilidad.EsInvertido(parameter))
        {
            positivo = !positivo;
        }

        return positivo ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Color del nivel de una alerta del dashboard.</summary>
public class NivelAlertaABrocha : IValueConverter
{
    private static readonly SolidColorBrush Critica = EstadoStockABrocha.Congelar("#E53935");
    private static readonly SolidColorBrush Advertencia = EstadoStockABrocha.Congelar("#FB8C00");
    private static readonly SolidColorBrush Informacion = EstadoStockABrocha.Congelar("#1E88E5");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            NivelAlerta.Critica => Critica,
            NivelAlerta.Advertencia => Advertencia,
            _ => Informacion
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Ancho del panel lateral según esté expandido o colapsado.</summary>
/// <summary>
/// Alineación del contenido del menú: centrada cuando solo se ve el icono, pegada
/// a la izquierda cuando el icono va acompañado de su texto.
/// </summary>
public class MenuColapsadoAAlineacion : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? HorizontalAlignment.Center : HorizontalAlignment.Left;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Tamaño del logotipo: más pequeño en la barra estrecha para que respire.</summary>
/// <summary>Margen de la cabecera: sin desplazamiento lateral cuando va centrada.</summary>
public class MenuColapsadoAMargenCabecera : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? new Thickness(0) : new Thickness(6, 0, 0, 0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public class MenuColapsadoATamanoLogo : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 34d : 38d;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public class MenuColapsadoAAncho : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 64d : 248d;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
