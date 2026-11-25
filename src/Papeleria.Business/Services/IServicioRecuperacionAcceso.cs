namespace Papeleria.Business.Services;

/// <summary>
/// Volver a entrar cuando se olvidó la contraseña.
///
/// PapelSoft funciona sin internet, así que no hay correo ni SMS a donde mandar un
/// enlace. En su lugar cada usuario tiene un código de recuperación, que genera él
/// mismo y guarda en papel, como los códigos de respaldo de Google o Microsoft. Con
/// ese código, desde la pantalla de acceso, se pone una contraseña nueva.
/// </summary>
public interface IServicioRecuperacionAcceso
{
    /// <summary>
    /// Genera un código nuevo para el usuario de la sesión y lo devuelve en claro: es la
    /// única vez que se puede ver. El anterior, si lo había, deja de servir.
    /// </summary>
    Task<string> GenerarCodigoAsync(CancellationToken ct = default);

    /// <summary>Cuándo generó su código el usuario; <c>null</c> si nunca lo ha hecho.</summary>
    DateTime? FechaCodigo(int usuarioId);

    /// <summary>
    /// Pone una contraseña nueva a quien presente su código de recuperación. El código
    /// usado se gasta y se entrega uno nuevo, que es lo que devuelve.
    /// </summary>
    Task<string> RestablecerConCodigoAsync(
        string nombreUsuario, string codigo, string contrasenaNueva, CancellationToken ct = default);
}
