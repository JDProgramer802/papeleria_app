namespace Papeleria.Domain.Security;

/// <summary>
/// Abstracción del algoritmo de hash de contraseñas. Permite que la capa de datos
/// siembre el usuario administrador sin depender de la implementación concreta.
/// </summary>
public interface IServicioHash
{
    /// <summary>Genera el hash de una contraseña en claro.</summary>
    string Generar(string contrasenaEnClaro);

    /// <summary>Comprueba una contraseña contra su hash almacenado.</summary>
    bool Verificar(string contrasenaEnClaro, string hashAlmacenado);
}
