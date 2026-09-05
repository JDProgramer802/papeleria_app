using System.ComponentModel.DataAnnotations;

namespace Papeleria.Domain.Enums;

/// <summary>Formas de pago aceptadas en el punto de venta.</summary>
public enum MetodoPago
{
    [Display(Name = "Efectivo")]
    Efectivo = 1,

    [Display(Name = "Tarjeta")]
    Tarjeta = 2,

    [Display(Name = "Transferencia")]
    Transferencia = 3,

    [Display(Name = "Crédito")]
    Credito = 4,

    [Display(Name = "Mixto")]
    Mixto = 5,

    // Billeteras digitales. Van aparte de «Transferencia» porque al cerrar el día hay
    // que saber cuánto entró a cada teléfono, y no entran al cajón.
    [Display(Name = "Nequi")]
    Nequi = 6,

    [Display(Name = "Daviplata")]
    Daviplata = 7
}

/// <summary>Estado de una factura de venta. Las ventas nunca se eliminan, solo se anulan.</summary>
public enum EstadoVenta
{
    [Display(Name = "Completada")]
    Completada = 1,

    [Display(Name = "Anulada")]
    Anulada = 2
}

/// <summary>
/// Estado de una cotización. «Vencida» no se guarda: se deduce de la fecha, así que
/// nunca queda una marcada como vigente cuando ya se le pasó el plazo.
/// </summary>
public enum EstadoCotizacion
{
    [Display(Name = "Vigente")]
    Vigente = 1,

    [Display(Name = "Aceptada")]
    Aceptada = 2,

    [Display(Name = "Rechazada")]
    Rechazada = 3
}

/// <summary>Estado de una compra registrada a un proveedor.</summary>
public enum EstadoCompra
{
    [Display(Name = "Registrada")]
    Registrada = 1,

    [Display(Name = "Anulada")]
    Anulada = 2
}

/// <summary>Tipos de documento de identificación usados en Colombia.</summary>
public enum TipoDocumento
{
    [Display(Name = "Cédula de ciudadanía")]
    CedulaCiudadania = 1,

    [Display(Name = "Cédula de extranjería")]
    CedulaExtranjeria = 2,

    [Display(Name = "NIT")]
    Nit = 3,

    [Display(Name = "Pasaporte")]
    Pasaporte = 4,

    [Display(Name = "Tarjeta de identidad")]
    TarjetaIdentidad = 5,

    [Display(Name = "Sin identificación")]
    SinIdentificacion = 6
}
