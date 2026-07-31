using Papeleria.Domain.Common;

namespace Papeleria.Domain.Entities;

/// <summary>
/// Almacén clave/valor de parámetros del sistema (datos de empresa, impuestos,
/// numeración, backups, preferencias de interfaz). Ver <see cref="Constants.ClavesConfiguracion"/>.
/// </summary>
public class Configuracion : EntidadBase
{
    public string Clave { get; set; } = string.Empty;

    public string? Valor { get; set; }

    public string? Descripcion { get; set; }
}
