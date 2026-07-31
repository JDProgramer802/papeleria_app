namespace Papeleria.Domain.Exceptions;

/// <summary>
/// Error previsible de reglas de negocio (stock insuficiente, caja cerrada, datos duplicados…).
/// La capa de presentación lo muestra tal cual al usuario, sin traza técnica.
/// </summary>
public class NegocioException : Exception
{
    public NegocioException(string mensaje) : base(mensaje) { }

    public NegocioException(string mensaje, Exception inner) : base(mensaje, inner) { }
}

/// <summary>El usuario autenticado no tiene permiso para la operación solicitada.</summary>
public class PermisoDenegadoException : NegocioException
{
    public PermisoDenegadoException(string mensaje) : base(mensaje) { }
}

/// <summary>No existe el registro solicitado.</summary>
public class RegistroNoEncontradoException : NegocioException
{
    public RegistroNoEncontradoException(string entidad, object clave)
        : base($"No se encontró {entidad} con identificador '{clave}'.") { }
}
