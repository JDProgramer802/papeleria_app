using Papeleria.Domain.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Domain.Entities;

/// <summary>Proveedor al que se le compra mercancía.</summary>
public class Proveedor : EntidadBase, IActivable
{
    public string Nombre { get; set; } = string.Empty;

    public string? Nit { get; set; }

    public string? Contacto { get; set; }

    public string? Telefono { get; set; }

    public string? Correo { get; set; }

    public string? Direccion { get; set; }

    public string? Ciudad { get; set; }

    public string? Observaciones { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<Compra> Compras { get; set; } = new List<Compra>();
}

/// <summary>Cliente de la papelería. Existe siempre un registro «Consumidor final».</summary>
public class Cliente : EntidadBase, IActivable
{
    public string Nombre { get; set; } = string.Empty;

    public TipoDocumento TipoDocumento { get; set; } = TipoDocumento.CedulaCiudadania;

    public string? NumeroDocumento { get; set; }

    public string? Telefono { get; set; }

    public string? Correo { get; set; }

    public string? Direccion { get; set; }

    public string? Ciudad { get; set; }

    public string? Observaciones { get; set; }

    /// <summary>
    /// Cupo máximo que se le puede fiar. En cero el cliente no acepta crédito,
    /// que es lo que conviene para el mostrador y el consumidor final.
    /// </summary>
    public decimal LimiteCredito { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>El «Consumidor final» no puede eliminarse: es el cliente por defecto del POS.</summary>
    public bool EsProtegido { get; set; }

    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();

    public ICollection<AbonoCliente> Abonos { get; set; } = new List<AbonoCliente>();
}
