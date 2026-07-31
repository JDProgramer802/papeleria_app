using Papeleria.Business.Security;

namespace Papeleria.Business.Services;

/// <summary>Autenticación de operadores y gestión de la contraseña propia.</summary>
public interface IServicioAutenticacion
{
    /// <summary>
    /// Valida las credenciales y devuelve la sesión con los permisos ya resueltos.
    /// Lanza <see cref="Domain.Exceptions.NegocioException"/> si el acceso no procede.
    /// </summary>
    Task<UsuarioSesion> AutenticarAsync(string nombreUsuario, string contrasena, CancellationToken ct = default);

    /// <summary>Cambia la contraseña verificando primero la actual.</summary>
    Task CambiarContrasenaAsync(int usuarioId, string contrasenaActual, string contrasenaNueva,
        CancellationToken ct = default);

    /// <summary>Nombre del último usuario que inició sesión, si se pidió recordarlo.</summary>
    string ObtenerUltimoUsuario();

    bool RecordarUsuarioActivo();

    Task GuardarPreferenciaUsuarioAsync(string nombreUsuario, bool recordar, CancellationToken ct = default);

    /// <summary>Indica si aún se está usando la contraseña que trae el sistema de fábrica.</summary>
    Task<bool> UsaContrasenaPorDefectoAsync(string nombreUsuario, CancellationToken ct = default);
}
