using System.Globalization;
using System.Text;

namespace Papeleria.Business.Common;

/// <summary>Utilidades de normalización de texto usadas en validaciones y búsquedas.</summary>
public static class Texto
{
    /// <summary>Convierte cadenas vacías o con solo espacios en <c>null</c>.</summary>
    public static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>Recorta y colapsa espacios de un campo obligatorio.</summary>
    public static string Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? string.Empty : valor.Trim();

    /// <summary>Elimina tildes y pasa a minúsculas, para comparaciones tolerantes.</summary>
    public static string SinTildes(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
        {
            return string.Empty;
        }

        var descompuesto = valor.Normalize(NormalizationForm.FormD);
        var constructor = new StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                constructor.Append(caracter);
            }
        }

        return constructor.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    /// <summary>Genera un nombre de archivo válido a partir de un texto libre.</summary>
    public static string NombreArchivoSeguro(string valor)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var limpio = new string(valor.Select(c => invalidos.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(limpio) ? "documento" : limpio.Trim();
    }

    /// <summary>Formatea un consecutivo con prefijo y relleno de ceros: <c>FV-000123</c>.</summary>
    public static string Consecutivo(string? prefijo, int numero, int longitud = 6) =>
        $"{prefijo}{numero.ToString().PadLeft(longitud, '0')}";
}
