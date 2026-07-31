using Papeleria.Domain.Common;

namespace Papeleria.Domain.Entities;

/// <summary>Clasificación comercial del producto (cuadernos, escritura, arte…).</summary>
public class Categoria : EntidadBase, IActivable
{
    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}

/// <summary>Fabricante o marca comercial del producto.</summary>
public class Marca : EntidadBase, IActivable
{
    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}

/// <summary>Unidad en que se comercializa el producto (unidad, caja, resma, paquete…).</summary>
public class UnidadMedida : EntidadBase, IActivable
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Abreviatura mostrada en facturas y grillas (UND, CJA, RSM…).</summary>
    public string Abreviatura { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
