using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Papeleria.Business.Security;
using Papeleria.Data.Repositories;
using Papeleria.Data.Seed;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Enums;
using Papeleria.Domain.Exceptions;
using Papeleria.Domain.Security;

namespace Papeleria.Business.Services;

/// <inheritdoc cref="IServicioAutenticacion" />
public class ServicioAutenticacion : IServicioAutenticacion
{
    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IServicioHash _hash;
    private readonly IServicioConfiguracion _configuracion;
    private readonly ILogger<ServicioAutenticacion> _log;

    public ServicioAutenticacion(
        IUnidadDeTrabajoFactory fabrica,
        IServicioHash hash,
        IServicioConfiguracion configuracion,
        ILogger<ServicioAutenticacion> log)
    {
        _fabrica = fabrica;
        _hash = hash;
        _configuracion = configuracion;
        _log = log;
    }

    public async Task<UsuarioSesion> AutenticarAsync(
        string nombreUsuario, string contrasena, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nombreUsuario) || string.IsNullOrWhiteSpace(contrasena))
        {
            throw new NegocioException("Escriba su usuario y su contraseña.");
        }

        nombreUsuario = nombreUsuario.Trim();

        await using var unidad = _fabrica.Crear();

        var usuario = await unidad.Contexto.Usuarios
            .FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario, ct)
            .ConfigureAwait(false);

        // Mismo mensaje para usuario inexistente y contraseña incorrecta:
        // no se revela qué parte de la credencial falló.
        if (usuario is null || !_hash.Verificar(contrasena, usuario.PasswordHash))
        {
            _log.LogWarning("Intento de acceso fallido para el usuario {Usuario}", nombreUsuario);
            throw new NegocioException("Usuario o contraseña incorrectos.");
        }

        if (!usuario.Activo)
        {
            throw new NegocioException("Este usuario está inactivo. Comuníquese con el administrador.");
        }

        usuario.UltimoAcceso = DateTime.Now;
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

        var permisos = await CargarPermisosAsync(unidad, usuario.Rol, ct).ConfigureAwait(false);

        _log.LogInformation("Sesión iniciada por {Usuario} ({Rol})", usuario.NombreUsuario, usuario.Rol);

        return new UsuarioSesion
        {
            Id = usuario.Id,
            NombreUsuario = usuario.NombreUsuario,
            NombreCompleto = usuario.NombreCompleto,
            Rol = usuario.Rol,
            Permisos = permisos
        };
    }

    /// <summary>Resuelve la matriz de permisos del rol; el administrador siempre tiene acceso total.</summary>
    private static async Task<IReadOnlyDictionary<string, PermisoEfectivo>> CargarPermisosAsync(
        IUnidadDeTrabajo unidad, RolUsuario rol, CancellationToken ct)
    {
        if (rol == RolUsuario.Administrador)
        {
            return Modulos.Todos.ToDictionary(m => m, _ => PermisoEfectivo.Total, StringComparer.OrdinalIgnoreCase);
        }

        var permisos = await unidad.Contexto.Permisos
            .AsNoTracking()
            .Where(p => p.Rol == rol)
            .ToListAsync(ct).ConfigureAwait(false);

        var mapa = new Dictionary<string, PermisoEfectivo>(StringComparer.OrdinalIgnoreCase);

        foreach (var modulo in Modulos.Todos)
        {
            var permiso = permisos.FirstOrDefault(
                p => string.Equals(p.Modulo, modulo, StringComparison.OrdinalIgnoreCase));

            mapa[modulo] = permiso is null
                ? PermisoEfectivo.Ninguno
                : new PermisoEfectivo(permiso.PuedeVer, permiso.PuedeCrear, permiso.PuedeEditar, permiso.PuedeEliminar);
        }

        return mapa;
    }

    public async Task CambiarContrasenaAsync(
        int usuarioId, string contrasenaActual, string contrasenaNueva, CancellationToken ct = default)
    {
        ValidarFortaleza(contrasenaNueva);

        await using var unidad = _fabrica.Crear();

        var usuario = await unidad.Contexto.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
                          .ConfigureAwait(false)
                      ?? throw new RegistroNoEncontradoException("el usuario", usuarioId);

        if (!_hash.Verificar(contrasenaActual, usuario.PasswordHash))
        {
            throw new NegocioException("La contraseña actual no es correcta.");
        }

        if (_hash.Verificar(contrasenaNueva, usuario.PasswordHash))
        {
            throw new NegocioException("La nueva contraseña debe ser distinta de la actual.");
        }

        usuario.PasswordHash = _hash.Generar(contrasenaNueva);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

        _log.LogInformation("El usuario {Usuario} cambió su contraseña", usuario.NombreUsuario);
    }

    /// <summary>Reglas mínimas de complejidad exigidas a cualquier contraseña nueva.</summary>
    internal static void ValidarFortaleza(string contrasena)
    {
        if (string.IsNullOrWhiteSpace(contrasena) || contrasena.Length < 6)
        {
            throw new NegocioException("La contraseña debe tener al menos 6 caracteres.");
        }

        if (!contrasena.Any(char.IsLetter) || !contrasena.Any(char.IsDigit))
        {
            throw new NegocioException("La contraseña debe combinar letras y números.");
        }
    }

    public string ObtenerUltimoUsuario() =>
        _configuracion.ObtenerTexto(ClavesConfiguracion.UltimoUsuario);

    public bool RecordarUsuarioActivo() =>
        _configuracion.ObtenerBooleano(ClavesConfiguracion.RecordarUsuario, true);

    public Task GuardarPreferenciaUsuarioAsync(string nombreUsuario, bool recordar, CancellationToken ct = default) =>
        _configuracion.GuardarVariosAsync(new Dictionary<string, string?>
        {
            [ClavesConfiguracion.RecordarUsuario] = recordar.ToString(),
            [ClavesConfiguracion.UltimoUsuario] = recordar ? nombreUsuario : string.Empty
        }, ct);

    public async Task<bool> UsaContrasenaPorDefectoAsync(string nombreUsuario, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        var hash = await unidad.Contexto.Usuarios
            .AsNoTracking()
            .Where(u => u.NombreUsuario == nombreUsuario)
            .Select(u => u.PasswordHash)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return hash is not null
               && _hash.Verificar(SembradorDatos.ContrasenaAdministradorPorDefecto, hash);
    }
}
