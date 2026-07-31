using Papeleria.Domain.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Domain.Entities;

/// <summary>Turno de caja: desde la apertura con base inicial hasta el arqueo de cierre.</summary>
public class CajaSesion : EntidadBase
{
    public DateTime FechaApertura { get; set; } = DateTime.Now;

    public DateTime? FechaCierre { get; set; }

    public int UsuarioAperturaId { get; set; }
    public Usuario? UsuarioApertura { get; set; }

    public int? UsuarioCierreId { get; set; }
    public Usuario? UsuarioCierre { get; set; }

    /// <summary>Base con la que se abre la caja.</summary>
    public decimal MontoInicial { get; set; }

    /// <summary>Efectivo que el sistema calcula que debería haber al cierre.</summary>
    public decimal MontoEsperado { get; set; }

    /// <summary>Efectivo realmente contado por el cajero.</summary>
    public decimal MontoReal { get; set; }

    /// <summary>Real − esperado. Negativo es faltante, positivo sobrante.</summary>
    public decimal Diferencia { get; set; }

    public decimal TotalVentasEfectivo { get; set; }

    public decimal TotalVentasOtros { get; set; }

    public decimal TotalIngresos { get; set; }

    public decimal TotalEgresos { get; set; }

    public int CantidadVentas { get; set; }

    public EstadoCajaSesion Estado { get; set; } = EstadoCajaSesion.Abierta;

    public string? ObservacionesApertura { get; set; }

    public string? ObservacionesCierre { get; set; }

    public ICollection<MovimientoCaja> Movimientos { get; set; } = new List<MovimientoCaja>();

    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
}

/// <summary>Entrada o salida de dinero dentro de una sesión de caja.</summary>
public class MovimientoCaja : EntidadBase
{
    public int CajaSesionId { get; set; }
    public CajaSesion? CajaSesion { get; set; }

    public DateTime Fecha { get; set; } = DateTime.Now;

    public TipoMovimientoCaja Tipo { get; set; }

    /// <summary>Monto siempre positivo; el signo lo determina <see cref="Tipo"/>.</summary>
    public decimal Monto { get; set; }

    public string Concepto { get; set; } = string.Empty;

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public int? VentaId { get; set; }
    public Venta? Venta { get; set; }

    /// <summary>Solo el efectivo afecta el arqueo; tarjeta y transferencia no suman al cajón.</summary>
    public bool AfectaEfectivo { get; set; } = true;
}
