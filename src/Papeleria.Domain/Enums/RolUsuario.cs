using System.ComponentModel.DataAnnotations;

namespace Papeleria.Domain.Enums;

/// <summary>Perfiles de acceso del sistema. Cada rol tiene permisos independientes por módulo.</summary>
public enum RolUsuario
{
    [Display(Name = "Administrador")]
    Administrador = 1,

    [Display(Name = "Cajero")]
    Cajero = 2,

    [Display(Name = "Bodega")]
    Bodega = 3
}
