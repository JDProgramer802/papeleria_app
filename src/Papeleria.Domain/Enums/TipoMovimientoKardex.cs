using System.ComponentModel.DataAnnotations;

namespace Papeleria.Domain.Enums;

/// <summary>Origen de cada movimiento registrado en el kardex.</summary>
public enum TipoMovimientoKardex
{
    [Display(Name = "Entrada por compra")]
    CompraEntrada = 1,

    [Display(Name = "Salida por venta")]
    VentaSalida = 2,

    [Display(Name = "Entrada manual")]
    EntradaManual = 3,

    [Display(Name = "Salida manual")]
    SalidaManual = 4,

    [Display(Name = "Ajuste positivo")]
    AjustePositivo = 5,

    [Display(Name = "Ajuste negativo")]
    AjusteNegativo = 6,

    [Display(Name = "Transferencia")]
    Transferencia = 7,

    [Display(Name = "Anulación de venta")]
    AnulacionVenta = 8,

    [Display(Name = "Anulación de compra")]
    AnulacionCompra = 9,

    [Display(Name = "Saldo inicial")]
    SaldoInicial = 10
}

/// <summary>Indica si un tipo de movimiento suma o resta existencias.</summary>
public enum NaturalezaMovimiento
{
    Entrada = 1,
    Salida = 2
}
