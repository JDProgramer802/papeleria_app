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
    Mixto = 5
}

/// <summary>Estado de una factura de venta. Las ventas nunca se eliminan, solo se anulan.</summary>
public enum EstadoVenta
{
    [Display(Name = "Completada")]
    Completada = 1,

    [Display(Name = "Anulada")]
    Anulada = 2
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
