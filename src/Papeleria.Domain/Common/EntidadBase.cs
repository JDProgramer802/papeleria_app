namespace Papeleria.Domain.Common;

/// <summary>
/// Raíz común de las entidades persistidas. Expone la clave primaria y las
/// marcas de auditoría que <c>AppDbContext</c> mantiene automáticamente.
/// </summary>
public abstract class EntidadBase
{
    public int Id { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    public DateTime? FechaModificacion { get; set; }
}

/// <summary>
/// Entidades de catálogo que pueden desactivarse en lugar de eliminarse
/// cuando ya participan en documentos históricos.
/// </summary>
public interface IActivable
{
    bool Activo { get; set; }
}
