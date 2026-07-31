using Papeleria.Domain.Entities;
using Papeleria.Domain.Enums;

namespace Papeleria.Business.Services;

/// <summary>Administración de operadores y de la matriz de permisos por rol.</summary>
public interface IServicioUsuarios
{
    Task<List<Usuario>> ListarAsync(bool incluirInactivos = true, CancellationToken ct = default);

    Task<Usuario?> ObtenerAsync(int id, CancellationToken ct = default);

    Task<Usuario> CrearAsync(Usuario usuario, string contrasena, CancellationToken ct = default);

    Task ActualizarAsync(Usuario usuario, CancellationToken ct = default);

    /// <summary>Restablece la contraseña sin pedir la anterior. Reservado al administrador.</summary>
    Task RestablecerContrasenaAsync(int usuarioId, string contrasenaNueva, CancellationToken ct = default);

    Task CambiarEstadoAsync(int usuarioId, bool activo, CancellationToken ct = default);

    Task EliminarAsync(int usuarioId, CancellationToken ct = default);

    Task<List<PermisoRol>> ObtenerPermisosAsync(RolUsuario rol, CancellationToken ct = default);

    Task GuardarPermisosAsync(IEnumerable<PermisoRol> permisos, CancellationToken ct = default);
}
