namespace Papeleria.Business.Common;

/// <summary>
/// Redondeo monetario centralizado. Todos los importes se guardan con dos decimales
/// usando redondeo bancario invertido (MidpointRounding.AwayFromZero), que es el que
/// espera un cajero al cuadrar el arqueo.
/// </summary>
public static class Dinero
{
    public const int Decimales = 2;

    public static decimal Redondear(decimal valor) =>
        Math.Round(valor, Decimales, MidpointRounding.AwayFromZero);

    public static decimal Redondear(double valor) =>
        Math.Round((decimal)valor, Decimales, MidpointRounding.AwayFromZero);

    /// <summary>Porcentaje aplicado sobre una base, ya redondeado.</summary>
    public static decimal Porcentaje(decimal baseCalculo, decimal porcentaje) =>
        Redondear(baseCalculo * porcentaje / 100m);

    /// <summary>Evita divisiones por cero en cálculos de margen y variación.</summary>
    public static decimal DividirSeguro(decimal numerador, decimal denominador) =>
        denominador == 0 ? 0 : Redondear(numerador / denominador);

    /// <summary>Variación porcentual entre dos periodos.</summary>
    public static decimal VariacionPorcentual(decimal actual, decimal anterior)
    {
        if (anterior == 0)
        {
            return actual == 0 ? 0 : 100m;
        }

        return Math.Round((actual - anterior) / Math.Abs(anterior) * 100m, 1, MidpointRounding.AwayFromZero);
    }
}
