using System.ComponentModel.DataAnnotations;

namespace Papeleria.Domain.Enums;

/// <summary>
/// Naturaleza de lo que se vende. Una papelería no solo mueve mercancía: las
/// fotocopias, las impresiones o el anillado se cobran igual pero no tienen
/// existencias que descontar ni movimientos de inventario que registrar.
/// </summary>
public enum TipoProducto
{
    [Display(Name = "Producto")]
    Producto = 1,

    [Display(Name = "Servicio")]
    Servicio = 2
}
