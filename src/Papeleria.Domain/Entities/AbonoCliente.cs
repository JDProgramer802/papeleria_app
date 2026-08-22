using Papeleria.Domain.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Domain.Entities;

/// <summary>
/// Pago que un cliente hace contra lo que debe. Los abonos van a la cuenta del
/// cliente, no a una factura concreta: en el mostrador se recibe «un abono de
/// tanto» y el sistema lo aplica a las facturas más antiguas al calcular la deuda.
/// </summary>
public class AbonoCliente : EntidadBase
{
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public DateTime Fecha { get; set; } = DateTime.Now;

    public decimal Monto { get; set; }

    public MetodoPago MetodoPago { get; set; } = MetodoPago.Efectivo;

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>Turno de caja en el que se recibió; nulo si se registró con la caja cerrada.</summary>
    public int? CajaSesionId { get; set; }
    public CajaSesion? CajaSesion { get; set; }

    public string? Observaciones { get; set; }

    /// <summary>Anular un abono deja rastro en lugar de borrar el registro.</summary>
    public bool Anulado { get; set; }

    public DateTime? FechaAnulacion { get; set; }

    public string? MotivoAnulacion { get; set; }
}
