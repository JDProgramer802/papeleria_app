using System.ComponentModel.DataAnnotations;

namespace Papeleria.Domain.Enums;

/// <summary>Estado de una sesión (turno) de caja.</summary>
public enum EstadoCajaSesion
{
    [Display(Name = "Abierta")]
    Abierta = 1,

    [Display(Name = "Cerrada")]
    Cerrada = 2
}

/// <summary>Naturaleza de cada movimiento dentro de una sesión de caja.</summary>
public enum TipoMovimientoCaja
{
    [Display(Name = "Apertura")]
    Apertura = 1,

    [Display(Name = "Venta")]
    Venta = 2,

    [Display(Name = "Ingreso")]
    Ingreso = 3,

    [Display(Name = "Egreso")]
    Egreso = 4,

    [Display(Name = "Anulación de venta")]
    AnulacionVenta = 5,

    [Display(Name = "Cierre")]
    Cierre = 6
}
