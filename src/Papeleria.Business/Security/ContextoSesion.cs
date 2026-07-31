using Papeleria.Domain.Constants;
using Papeleria.Domain.Enums;
using Papeleria.Domain.Exceptions;

namespace Papeleria.Business.Security;

/// <summary>Acciones que se pueden autorizar sobre un módulo.</summary>
public enum AccionPermiso
{
    Ver = 1,
    Crear = 2,
    Editar = 3,
    Eliminar = 4
}

/// <summary>Datos del usuario autenticado y sus permisos efectivos.</summary>
public class UsuarioSesion
{
    public required int Id { get; init; }

    public required string NombreUsuario { get; init; }

    public required string NombreCompleto { get; init; }

    public required RolUsuario Rol { get; init; }

    public string RolTexto => Rol switch
    {
        RolUsuario.Administrador => "Administrador",
        RolUsuario.Cajero => "Cajero",
        RolUsuario.Bodega => "Bodega",
        _ => Rol.ToString()
    };

    /// <summary>Iniciales para el avatar de la barra superior.</summary>
    public string Iniciales
    {
        get
        {
            var partes = NombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return partes.Length switch
            {
                0 => "?",
                1 => partes[0][..1].ToUpperInvariant(),
                _ => (partes[0][..1] + partes[1][..1]).ToUpperInvariant()
            };
        }
    }

    /// <summary>Permisos efectivos indexados por módulo.</summary>
    public required IReadOnlyDictionary<string, PermisoEfectivo> Permisos { get; init; }
}

/// <summary>Permisos concretos de un rol sobre un módulo.</summary>
public record PermisoEfectivo(bool Ver, bool Crear, bool Editar, bool Eliminar)
{
    public static readonly PermisoEfectivo Ninguno = new(false, false, false, false);

    public static readonly PermisoEfectivo Total = new(true, true, true, true);

    public bool Permite(AccionPermiso accion) => accion switch
    {
        AccionPermiso.Ver => Ver,
        AccionPermiso.Crear => Crear,
        AccionPermiso.Editar => Editar,
        AccionPermiso.Eliminar => Eliminar,
        _ => false
    };
}

/// <summary>Sesión de trabajo activa. Es un servicio único durante toda la vida de la aplicación.</summary>
public interface IContextoSesion
{
    UsuarioSesion? Usuario { get; }

    bool EstaAutenticado { get; }

    /// <summary>Identificador del usuario actual; lanza si no hay sesión iniciada.</summary>
    int UsuarioIdRequerido { get; }

    event EventHandler? SesionCambiada;

    void Iniciar(UsuarioSesion usuario);

    void Cerrar();

    bool Puede(string modulo, AccionPermiso accion = AccionPermiso.Ver);

    /// <summary>Lanza <see cref="PermisoDenegadoException"/> si el usuario no está autorizado.</summary>
    void Exigir(string modulo, AccionPermiso accion);

    bool EsAdministrador { get; }
}

/// <inheritdoc cref="IContextoSesion" />
public class ContextoSesion : IContextoSesion
{
    private UsuarioSesion? _usuario;

    public UsuarioSesion? Usuario => _usuario;

    public bool EstaAutenticado => _usuario is not null;

    public bool EsAdministrador => _usuario?.Rol == RolUsuario.Administrador;

    public int UsuarioIdRequerido =>
        _usuario?.Id ?? throw new NegocioException("No hay una sesión de usuario activa.");

    public event EventHandler? SesionCambiada;

    public void Iniciar(UsuarioSesion usuario)
    {
        _usuario = usuario;
        SesionCambiada?.Invoke(this, EventArgs.Empty);
    }

    public void Cerrar()
    {
        _usuario = null;
        SesionCambiada?.Invoke(this, EventArgs.Empty);
    }

    public bool Puede(string modulo, AccionPermiso accion = AccionPermiso.Ver)
    {
        if (_usuario is null)
        {
            return false;
        }

        // El administrador conserva acceso total aunque se editen las matrices de permisos.
        if (_usuario.Rol == RolUsuario.Administrador)
        {
            return true;
        }

        return _usuario.Permisos.TryGetValue(modulo, out var permiso) && permiso.Permite(accion);
    }

    public void Exigir(string modulo, AccionPermiso accion)
    {
        if (Puede(modulo, accion))
        {
            return;
        }

        var nombreModulo = Modulos.Nombres.TryGetValue(modulo, out var nombre) ? nombre : modulo;
        var verbo = accion switch
        {
            AccionPermiso.Crear => "crear registros en",
            AccionPermiso.Editar => "modificar registros de",
            AccionPermiso.Eliminar => "eliminar registros de",
            _ => "consultar"
        };

        throw new PermisoDenegadoException(
            $"Su usuario no tiene permiso para {verbo} el módulo «{nombreModulo}».");
    }
}
