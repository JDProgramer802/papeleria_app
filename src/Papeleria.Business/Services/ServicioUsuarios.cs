using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Papeleria.Business.Common;
using Papeleria.Business.Security;
using Papeleria.Data.Repositories;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Entities;
using Papeleria.Domain.Enums;
using Papeleria.Domain.Exceptions;
using Papeleria.Domain.Security;

namespace Papeleria.Business.Services;

/// <inheritdoc cref="IServicioUsuarios" />
public class ServicioUsuarios : IServicioUsuarios
{
    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IServicioHash _hash;
    private readonly IContextoSesion _sesion;
    private readonly ILogger<ServicioUsuarios> _log;

    public ServicioUsuarios(
        IUnidadDeTrabajoFactory fabrica,
        IServicioHash hash,
        IContextoSesion sesion,
        ILogger<ServicioUsuarios> log)
    {
        _fabrica = fabrica;
        _hash = hash;
        _sesion = sesion;
        _log = log;
    }

    public async Task<List<Usuario>> ListarAsync(bool incluirInactivos = true, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Usuarios, AccionPermiso.Ver);

        await using var unidad = _fabrica.Crear();

        var consulta = unidad.Contexto.Usuarios.AsNoTracking();

        if (!incluirInactivos)
        {
            consulta = consulta.Where(u => u.Activo);
        }

        return await consulta
            .OrderBy(u => u.NombreCompleto)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<Usuario?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();
        return await unidad.Contexto.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<Usuario> CrearAsync(Usuario usuario, string contrasena, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Usuarios, AccionPermiso.Crear);
        ValidarDatos(usuario);
        ServicioAutenticacion.ValidarFortaleza(contrasena);

        await using var unidad = _fabrica.Crear();

        var nombreUsuario = usuario.NombreUsuario.Trim().ToLowerInvariant();

        if (await unidad.Contexto.Usuarios.AnyAsync(u => u.NombreUsuario == nombreUsuario, ct).ConfigureAwait(false))
        {
            throw new NegocioException($"Ya existe un usuario con el nombre «{nombreUsuario}».");
        }

        var nuevo = new Usuario
        {
            NombreUsuario = nombreUsuario,
            NombreCompleto = Texto.Normalizar(usuario.NombreCompleto),
            Correo = Texto.NormalizarOpcional(usuario.Correo),
            Telefono = Texto.NormalizarOpcional(usuario.Telefono),
            Rol = usuario.Rol,
            Activo = usuario.Activo,
            PasswordHash = _hash.Generar(contrasena)
        };

        unidad.Contexto.Usuarios.Add(nuevo);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

        _log.LogInformation("Usuario {Usuario} creado con rol {Rol}", nuevo.NombreUsuario, nuevo.Rol);
        return nuevo;
    }

    public async Task ActualizarAsync(Usuario usuario, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Usuarios, AccionPermiso.Editar);
        ValidarDatos(usuario);

        await using var unidad = _fabrica.Crear();

        var actual = await unidad.Contexto.Usuarios.FirstOrDefaultAsync(u => u.Id == usuario.Id, ct)
                         .ConfigureAwait(false)
                     ?? throw new RegistroNoEncontradoException("el usuario", usuario.Id);

        var nombreUsuario = usuario.NombreUsuario.Trim().ToLowerInvariant();

        if (await unidad.Contexto.Usuarios
                .AnyAsync(u => u.NombreUsuario == nombreUsuario && u.Id != usuario.Id, ct).ConfigureAwait(false))
        {
            throw new NegocioException($"Ya existe otro usuario con el nombre «{nombreUsuario}».");
        }

        // El administrador protegido no puede quedar sin acceso ni desactivado.
        if (actual.EsProtegido)
        {
            if (usuario.Rol != RolUsuario.Administrador)
            {
                throw new NegocioException("El administrador principal debe conservar el rol de administrador.");
            }

            if (!usuario.Activo)
            {
                throw new NegocioException("El administrador principal no puede desactivarse.");
            }
        }

        actual.NombreUsuario = nombreUsuario;
        actual.NombreCompleto = Texto.Normalizar(usuario.NombreCompleto);
        actual.Correo = Texto.NormalizarOpcional(usuario.Correo);
        actual.Telefono = Texto.NormalizarOpcional(usuario.Telefono);
        actual.Rol = usuario.Rol;
        actual.Activo = usuario.Activo;

        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
        _log.LogInformation("Usuario {Usuario} actualizado", actual.NombreUsuario);
    }

    public async Task RestablecerContrasenaAsync(int usuarioId, string contrasenaNueva, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Usuarios, AccionPermiso.Editar);
        ServicioAutenticacion.ValidarFortaleza(contrasenaNueva);

        await using var unidad = _fabrica.Crear();

        var usuario = await unidad.Contexto.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
                          .ConfigureAwait(false)
                      ?? throw new RegistroNoEncontradoException("el usuario", usuarioId);

        usuario.PasswordHash = _hash.Generar(contrasenaNueva);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

        _log.LogWarning("Contraseña restablecida para {Usuario} por {Administrador}",
            usuario.NombreUsuario, _sesion.Usuario?.NombreUsuario);
    }

    public async Task CambiarEstadoAsync(int usuarioId, bool activo, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Usuarios, AccionPermiso.Editar);

        await using var unidad = _fabrica.Crear();

        var usuario = await unidad.Contexto.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
                          .ConfigureAwait(false)
                      ?? throw new RegistroNoEncontradoException("el usuario", usuarioId);

        if (usuario.EsProtegido && !activo)
        {
            throw new NegocioException("El administrador principal no puede desactivarse.");
        }

        if (usuarioId == _sesion.Usuario?.Id && !activo)
        {
            throw new NegocioException("No puede desactivar el usuario con el que está trabajando.");
        }

        usuario.Activo = activo;
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
    }

    public async Task EliminarAsync(int usuarioId, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Usuarios, AccionPermiso.Eliminar);

        await using var unidad = _fabrica.Crear();

        var usuario = await unidad.Contexto.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
                          .ConfigureAwait(false)
                      ?? throw new RegistroNoEncontradoException("el usuario", usuarioId);

        if (usuario.EsProtegido)
        {
            throw new NegocioException("El administrador principal no puede eliminarse.");
        }

        if (usuarioId == _sesion.Usuario?.Id)
        {
            throw new NegocioException("No puede eliminar el usuario con el que está trabajando.");
        }

        // Un usuario con documentos asociados se desactiva: eliminarlo rompería el histórico.
        var tieneMovimientos =
            await unidad.Contexto.Ventas.AnyAsync(v => v.UsuarioId == usuarioId, ct).ConfigureAwait(false)
            || await unidad.Contexto.Compras.AnyAsync(c => c.UsuarioId == usuarioId, ct).ConfigureAwait(false)
            || await unidad.Contexto.MovimientosKardex.AnyAsync(m => m.UsuarioId == usuarioId, ct).ConfigureAwait(false)
            || await unidad.Contexto.CajaSesiones
                .AnyAsync(s => s.UsuarioAperturaId == usuarioId || s.UsuarioCierreId == usuarioId, ct)
                .ConfigureAwait(false);

        if (tieneMovimientos)
        {
            usuario.Activo = false;
            await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

            throw new NegocioException(
                "El usuario tiene movimientos registrados y no puede eliminarse sin perder el histórico. " +
                "Se desactivó para impedirle el acceso.");
        }

        unidad.Contexto.Usuarios.Remove(usuario);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

        _log.LogWarning("Usuario {Usuario} eliminado por {Administrador}",
            usuario.NombreUsuario, _sesion.Usuario?.NombreUsuario);
    }

    public async Task<List<PermisoRol>> ObtenerPermisosAsync(RolUsuario rol, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Usuarios, AccionPermiso.Ver);

        await using var unidad = _fabrica.Crear();

        var existentes = await unidad.Contexto.Permisos
            .AsNoTracking()
            .Where(p => p.Rol == rol)
            .ToListAsync(ct).ConfigureAwait(false);

        // Se completan los módulos que aún no tengan fila para que la matriz salga siempre completa.
        var resultado = new List<PermisoRol>(Modulos.Todos.Count);

        foreach (var modulo in Modulos.Todos)
        {
            resultado.Add(existentes.FirstOrDefault(
                              p => string.Equals(p.Modulo, modulo, StringComparison.OrdinalIgnoreCase))
                          ?? new PermisoRol { Rol = rol, Modulo = modulo });
        }

        return resultado;
    }

    public async Task GuardarPermisosAsync(IEnumerable<PermisoRol> permisos, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Usuarios, AccionPermiso.Editar);

        var lista = permisos.ToList();

        if (lista.Count == 0)
        {
            return;
        }

        if (lista.Any(p => p.Rol == RolUsuario.Administrador))
        {
            throw new NegocioException("Los permisos del rol Administrador no pueden restringirse.");
        }

        await using var unidad = _fabrica.Crear();

        var roles = lista.Select(p => p.Rol).Distinct().ToList();

        var existentes = await unidad.Contexto.Permisos
            .Where(p => roles.Contains(p.Rol))
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var permiso in lista)
        {
            var actual = existentes.FirstOrDefault(
                p => p.Rol == permiso.Rol &&
                     string.Equals(p.Modulo, permiso.Modulo, StringComparison.OrdinalIgnoreCase));

            if (actual is null)
            {
                unidad.Contexto.Permisos.Add(new PermisoRol
                {
                    Rol = permiso.Rol,
                    Modulo = permiso.Modulo,
                    PuedeVer = permiso.PuedeVer,
                    PuedeCrear = permiso.PuedeCrear,
                    PuedeEditar = permiso.PuedeEditar,
                    PuedeEliminar = permiso.PuedeEliminar
                });

                continue;
            }

            actual.PuedeVer = permiso.PuedeVer;
            actual.PuedeCrear = permiso.PuedeCrear;
            actual.PuedeEditar = permiso.PuedeEditar;
            actual.PuedeEliminar = permiso.PuedeEliminar;
        }

        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
        _log.LogInformation("Permisos actualizados para los roles {Roles}", string.Join(", ", roles));
    }

    private static void ValidarDatos(Usuario usuario)
    {
        if (string.IsNullOrWhiteSpace(usuario.NombreUsuario) || usuario.NombreUsuario.Trim().Length < 3)
        {
            throw new NegocioException("El nombre de usuario debe tener al menos 3 caracteres.");
        }

        if (usuario.NombreUsuario.Contains(' '))
        {
            throw new NegocioException("El nombre de usuario no puede contener espacios.");
        }

        if (string.IsNullOrWhiteSpace(usuario.NombreCompleto))
        {
            throw new NegocioException("Escriba el nombre completo del usuario.");
        }

        if (!string.IsNullOrWhiteSpace(usuario.Correo) && !usuario.Correo.Contains('@'))
        {
            throw new NegocioException("El correo electrónico no tiene un formato válido.");
        }
    }
}
