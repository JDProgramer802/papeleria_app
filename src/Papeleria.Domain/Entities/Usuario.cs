using Papeleria.Domain.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Domain.Entities;

/// <summary>Operador del sistema. La contraseña se almacena siempre como hash BCrypt.</summary>
public class Usuario : EntidadBase, IActivable
{
    public string NombreUsuario { get; set; } = string.Empty;

    public string NombreCompleto { get; set; } = string.Empty;

    /// <summary>Hash BCrypt. Nunca se guarda la contraseña en claro.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public RolUsuario Rol { get; set; } = RolUsuario.Cajero;

    public string? Correo { get; set; }

    public string? Telefono { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>El administrador raíz no puede eliminarse ni desactivarse.</summary>
    public bool EsProtegido { get; set; }

    public DateTime? UltimoAcceso { get; set; }

    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();

    public ICollection<Compra> Compras { get; set; } = new List<Compra>();

    public ICollection<MovimientoKardex> MovimientosKardex { get; set; } = new List<MovimientoKardex>();
}

/// <summary>
/// Permiso de un rol sobre un módulo. Existe una fila por combinación rol/módulo,
/// lo que permite al administrador ajustar el acceso sin recompilar.
/// </summary>
public class PermisoRol : EntidadBase
{
    public RolUsuario Rol { get; set; }

    /// <summary>Clave del módulo, ver <see cref="Constants.Modulos"/>.</summary>
    public string Modulo { get; set; } = string.Empty;

    public bool PuedeVer { get; set; }

    public bool PuedeCrear { get; set; }

    public bool PuedeEditar { get; set; }

    public bool PuedeEliminar { get; set; }
}
