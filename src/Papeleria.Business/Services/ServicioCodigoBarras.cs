using Papeleria.Business.Common;
using Papeleria.Domain.Exceptions;
using ZXing;
using ZXing.Common;

namespace Papeleria.Business.Services;

/// <summary>Simbologías admitidas al imprimir etiquetas.</summary>
public enum SimbologiaCodigoBarras
{
    /// <summary>Elige EAN-13 si el contenido lo permite; si no, Code 128.</summary>
    Automatica = 0,
    Code128 = 1,
    Ean13 = 2,
    QrCode = 3
}

/// <summary>Generación de imágenes de código de barras para pantalla y etiquetas.</summary>
public interface IServicioCodigoBarras
{
    /// <summary>Devuelve la imagen del código en formato PNG.</summary>
    byte[] GenerarPng(string contenido, SimbologiaCodigoBarras simbologia = SimbologiaCodigoBarras.Automatica,
        int ancho = 380, int alto = 120);

    /// <summary>Indica si el texto es un EAN-13 válido, dígito de control incluido.</summary>
    bool EsEan13Valido(string? contenido);
}

/// <inheritdoc cref="IServicioCodigoBarras" />
public class ServicioCodigoBarras : IServicioCodigoBarras
{
    public byte[] GenerarPng(
        string contenido,
        SimbologiaCodigoBarras simbologia = SimbologiaCodigoBarras.Automatica,
        int ancho = 380,
        int alto = 120)
    {
        if (string.IsNullOrWhiteSpace(contenido))
        {
            throw new NegocioException("No hay contenido para generar el código de barras.");
        }

        var texto = contenido.Trim();
        var formato = ResolverFormato(texto, simbologia);

        var opciones = new EncodingOptions
        {
            Width = Math.Max(ancho, 80),
            Height = Math.Max(alto, 40),
            Margin = formato == BarcodeFormat.QR_CODE ? 1 : 8,
            PureBarcode = true
        };

        try
        {
            var matriz = new MultiFormatWriter().encode(
                texto, formato, opciones.Width, opciones.Height, opciones.Hints);

            return ConvertirAPng(matriz);
        }
        catch (Exception ex) when (ex is not NegocioException)
        {
            throw new NegocioException(
                $"No se pudo generar el código de barras para «{texto}». " +
                "Verifique que el contenido sea compatible con la simbología elegida.", ex);
        }
    }

    /// <summary>
    /// EAN-13 solo admite 13 dígitos con checksum correcto; cualquier otro contenido
    /// se codifica en Code 128, que acepta texto alfanumérico.
    /// </summary>
    private BarcodeFormat ResolverFormato(string contenido, SimbologiaCodigoBarras simbologia) => simbologia switch
    {
        SimbologiaCodigoBarras.QrCode => BarcodeFormat.QR_CODE,
        SimbologiaCodigoBarras.Code128 => BarcodeFormat.CODE_128,
        SimbologiaCodigoBarras.Ean13 => EsEan13Valido(contenido)
            ? BarcodeFormat.EAN_13
            : throw new NegocioException(
                "El contenido no es un EAN-13 válido: se requieren 13 dígitos con dígito de control correcto."),
        _ => EsEan13Valido(contenido) ? BarcodeFormat.EAN_13 : BarcodeFormat.CODE_128
    };

    public bool EsEan13Valido(string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido))
        {
            return false;
        }

        var texto = contenido.Trim();

        if (texto.Length != 13 || !texto.All(char.IsAsciiDigit))
        {
            return false;
        }

        var esperado = ServicioProductos.CalcularDigitoVerificadorEan13(texto[..12]);
        return esperado == texto[12] - '0';
    }

    /// <summary>Traduce la matriz de módulos de ZXing a una imagen PNG en escala de grises.</summary>
    private static byte[] ConvertirAPng(BitMatrix matriz)
    {
        var ancho = matriz.Width;
        var alto = matriz.Height;
        var pixeles = new byte[ancho * alto];

        for (var y = 0; y < alto; y++)
        {
            var desplazamiento = y * ancho;

            for (var x = 0; x < ancho; x++)
            {
                pixeles[desplazamiento + x] = matriz[x, y] ? (byte)0 : (byte)255;
            }
        }

        return CodificadorPng.CodificarEscalaDeGrises(pixeles, ancho, alto);
    }
}
