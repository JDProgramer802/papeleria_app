using System.Text;

namespace Papeleria.Business.Common;

/// <summary>
/// Valor de una factura escrito en letras, como se acostumbra al pie de las facturas
/// colombianas: «SON: QUINCE MIL CUATROCIENTOS PESOS M/CTE».
///
/// Es lo que se lee cuando hay una cifra borrosa en la tirilla o una discusión sobre
/// un cero de más, y lo primero que busca el contador en un soporte.
/// </summary>
public static class NumeroEnLetras
{
    private static readonly string[] Unidades =
    {
        "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE",
        "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE",
        "DIECIOCHO", "DIECINUEVE", "VEINTE", "VEINTIUNO", "VEINTIDÓS", "VEINTITRÉS",
        "VEINTICUATRO", "VEINTICINCO", "VEINTISÉIS", "VEINTISIETE", "VEINTIOCHO", "VEINTINUEVE"
    };

    private static readonly string[] Decenas =
    {
        "", "", "", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"
    };

    private static readonly string[] Centenas =
    {
        "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS",
        "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"
    };

    /// <summary>
    /// «QUINCE MIL CUATROCIENTOS PESOS M/CTE». Los centavos, si los hay, van como
    /// fracción: «… PESOS CON 50/100 M/CTE».
    /// </summary>
    public static string Pesos(decimal valor)
    {
        var negativo = valor < 0;
        valor = Math.Abs(Math.Round(valor, 2, MidpointRounding.AwayFromZero));

        var entero = (long)Math.Truncate(valor);
        var centavos = (int)((valor - entero) * 100);

        var texto = new StringBuilder();

        if (negativo)
        {
            texto.Append("MENOS ");
        }

        texto.Append(entero == 0 ? "CERO" : Apocopar(Convertir(entero)));

        // «Un millón de pesos», pero «un millón cien mil pesos».
        texto.Append(entero >= 1_000_000 && entero % 1_000_000 == 0 ? " DE PESOS" : entero == 1 ? " PESO" : " PESOS");

        if (centavos > 0)
        {
            texto.Append($" CON {centavos:00}/100");
        }

        texto.Append(" M/CTE");
        return texto.ToString();
    }

    /// <summary>El número entero en letras, en mayúsculas y sin apocopar.</summary>
    public static string Convertir(long numero)
    {
        if (numero == 0)
        {
            return "CERO";
        }

        var partes = new List<string>();

        var billones = numero / 1_000_000_000_000;
        var millones = numero / 1_000_000 % 1_000_000;
        var miles = numero / 1_000 % 1_000;
        var resto = numero % 1_000;

        if (billones > 0)
        {
            partes.Add(billones == 1 ? "UN BILLÓN" : $"{Apocopar(Convertir(billones))} BILLONES");
        }

        if (millones > 0)
        {
            partes.Add(millones == 1 ? "UN MILLÓN" : $"{Apocopar(MilesYUnidades(millones))} MILLONES");
        }

        if (miles > 0)
        {
            partes.Add(miles == 1 ? "MIL" : $"{Apocopar(Centena(miles))} MIL");
        }

        if (resto > 0)
        {
            partes.Add(Centena(resto));
        }

        return string.Join(" ", partes);
    }

    /// <summary>Hasta 999.999, para los millones («doscientos mil millones»).</summary>
    private static string MilesYUnidades(long numero)
    {
        var miles = numero / 1_000;
        var resto = numero % 1_000;

        var partes = new List<string>();

        if (miles > 0)
        {
            partes.Add(miles == 1 ? "MIL" : $"{Apocopar(Centena(miles))} MIL");
        }

        if (resto > 0)
        {
            partes.Add(Centena(resto));
        }

        return string.Join(" ", partes);
    }

    /// <summary>De 1 a 999.</summary>
    private static string Centena(long numero)
    {
        if (numero == 100)
        {
            return "CIEN";
        }

        var centena = (int)(numero / 100);
        var decena = (int)(numero % 100);

        var partes = new List<string>();

        if (centena > 0)
        {
            partes.Add(Centenas[centena]);
        }

        if (decena > 0)
        {
            partes.Add(Decena(decena));
        }

        return string.Join(" ", partes);
    }

    /// <summary>De 1 a 99.</summary>
    private static string Decena(int numero)
    {
        if (numero < 30)
        {
            return Unidades[numero];
        }

        var unidad = numero % 10;
        return unidad == 0 ? Decenas[numero / 10] : $"{Decenas[numero / 10]} Y {Unidades[unidad]}";
    }

    /// <summary>
    /// Delante de un sustantivo «uno» se vuelve «un»: «veintiún mil», «treinta y un pesos».
    /// Solo se toca la última palabra, que es la que queda pegada al sustantivo.
    /// </summary>
    private static string Apocopar(string texto)
    {
        if (texto.EndsWith("VEINTIUNO", StringComparison.Ordinal))
        {
            return texto[..^"VEINTIUNO".Length] + "VEINTIÚN";
        }

        if (texto.EndsWith("UNO", StringComparison.Ordinal))
        {
            return texto[..^"UNO".Length] + "UN";
        }

        return texto;
    }
}
