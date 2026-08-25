using Papeleria.Domain.Common;

namespace Papeleria.Domain.Entities;

/// <summary>
/// Devolución de parte de una venta. En una papelería devolver es cosa de todos los
/// días —sobre todo en temporada escolar— y anular la factura entera para rehacerla
/// rompe el consecutivo y ensucia el histórico. Aquí solo vuelven los renglones que
/// el cliente trajo de regreso.
/// </summary>
public class Devolucion : EntidadBase
{
    public string Numero { get; set; } = string.Empty;

    public int VentaId { get; set; }
    public Venta? Venta { get; set; }

    public DateTime Fecha { get; set; } = DateTime.Now;

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>Turno de caja del que sale el dinero devuelto.</summary>
    public int? CajaSesionId { get; set; }
    public CajaSesion? CajaSesion { get; set; }

    public string Motivo { get; set; } = string.Empty;

    /// <summary>Importe reintegrado al cliente.</summary>
    public decimal Total { get; set; }

    /// <summary>Costo de lo devuelto, para deshacer la utilidad de esas líneas.</summary>
    public decimal CostoTotal { get; set; }

    public ICollection<DevolucionDetalle> Detalles { get; set; } = new List<DevolucionDetalle>();
}

/// <summary>Renglón devuelto, con la cantidad y el precio al que se había vendido.</summary>
public class DevolucionDetalle : EntidadBase
{
    public int DevolucionId { get; set; }
    public Devolucion? Devolucion { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public string DescripcionProducto { get; set; } = string.Empty;

    public decimal Cantidad { get; set; }

    /// <summary>Precio unitario neto al que se vendió, con su descuento ya aplicado.</summary>
    public decimal ValorUnitario { get; set; }

    public decimal CostoUnitario { get; set; }

    public decimal Total { get; set; }
}
