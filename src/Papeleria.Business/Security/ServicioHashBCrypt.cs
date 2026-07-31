using Papeleria.Domain.Security;

namespace Papeleria.Business.Security;

/// <summary>
/// Implementación de <see cref="IServicioHash"/> basada en BCrypt con sal aleatoria
/// por contraseña. El factor de trabajo 12 equilibra seguridad y tiempo de respuesta
/// en equipos de mostrador.
/// </summary>
public class ServicioHashBCrypt : IServicioHash
{
    private const int FactorTrabajo = 12;

    public string Generar(string contrasenaEnClaro)
    {
        if (string.IsNullOrWhiteSpace(contrasenaEnClaro))
        {
            throw new ArgumentException("La contraseña no puede estar vacía.", nameof(contrasenaEnClaro));
        }

        return BCrypt.Net.BCrypt.HashPassword(contrasenaEnClaro, FactorTrabajo);
    }

    public bool Verificar(string contrasenaEnClaro, string hashAlmacenado)
    {
        if (string.IsNullOrWhiteSpace(contrasenaEnClaro) || string.IsNullOrWhiteSpace(hashAlmacenado))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(contrasenaEnClaro, hashAlmacenado);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Hash corrupto o generado con otro algoritmo: se trata como credencial inválida.
            return false;
        }
    }
}
