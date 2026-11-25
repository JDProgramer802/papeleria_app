using Papeleria.Business.Dtos;

namespace Papeleria.App.Impresion;

/// <summary>
/// Lo que la tirilla necesita para dibujarse, ya masticado.
///
/// Los textos salen de <see cref="ReciboVenta"/>, el mismo que arma el PDF: la
/// tirilla y el PDF tienen que decir exactamente lo mismo. Aquí solo se añade lo
/// que es propio de imprimir desde WPF, como la imagen del código de barras.
/// </summary>
public class ReciboTicketDatos
{
    public required ReciboVenta Recibo { get; init; }

    /// <summary>Code 128 con el número de la factura, para buscarla escaneando la tirilla.</summary>
    public byte[]? CodigoBarras { get; init; }

    public string NombreEmpresa => Recibo.Empresa.Nombre.ToUpperInvariant();

    public string? Logo => Recibo.Empresa.TieneLogo ? Recibo.Empresa.LogoPath : null;

    public bool TieneResolucion => Recibo.Resolucion.Count > 0;

    /// <summary>
    /// WPF parte la línea en cualquier «/» y dejaba «M/» en un renglón y «CTE» en el
    /// siguiente. El carácter invisible de unión (U+2060) los mantiene juntos.
    /// </summary>
    public string TotalEnLetras => Recibo.TotalEnLetras.Replace("M/CTE", "M/⁠CTE");
}
