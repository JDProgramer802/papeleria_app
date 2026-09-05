using Papeleria.Business.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Business.Dtos;

/// <summary>Cálculo del efectivo que debería haber en caja en un momento dado.</summary>
public class ArqueoCajaDto
{
    public int CajaSesionId { get; init; }

    public DateTime FechaApertura { get; init; }

    public string UsuarioApertura { get; init; } = string.Empty;

    public decimal MontoInicial { get; init; }

    public decimal VentasEfectivo { get; init; }

    public decimal VentasTarjeta { get; init; }

    public decimal VentasTransferencia { get; init; }

    public decimal VentasCredito { get; init; }

    /// <summary>Cobrado por billeteras digitales. No entra al cajón, pero hay que cuadrarlo.</summary>
    public decimal VentasNequi { get; init; }

    public decimal VentasDaviplata { get; init; }

    public bool HayBilleteras => VentasNequi > 0 || VentasDaviplata > 0;

    /// <summary>De qué se compone «otros medios», para no tener que abrir el reporte.</summary>
    public string DesgloseOtrosMedios
    {
        get
        {
            var partes = new List<string>();

            if (VentasTarjeta > 0) partes.Add($"Tarjeta {Formatos.Moneda(VentasTarjeta)}");
            if (VentasTransferencia > 0) partes.Add($"Transferencia {Formatos.Moneda(VentasTransferencia)}");
            if (VentasNequi > 0) partes.Add($"Nequi {Formatos.Moneda(VentasNequi)}");
            if (VentasDaviplata > 0) partes.Add($"Daviplata {Formatos.Moneda(VentasDaviplata)}");
            if (VentasCredito > 0) partes.Add($"Crédito {Formatos.Moneda(VentasCredito)}");

            return partes.Count == 0
                ? "Todo se cobró en efectivo"
                : string.Join(Environment.NewLine, partes);
        }
    }

    public decimal Ingresos { get; init; }

    public decimal Egresos { get; init; }

    /// <summary>Efectivo devuelto por anular ventas cobradas en sesiones anteriores.</summary>
    public decimal Devoluciones { get; init; }

    public bool HayDevoluciones => Devoluciones > 0;

    public int CantidadVentas { get; init; }

    /// <summary>Total facturado en la sesión, con independencia del medio de pago.</summary>
    public decimal TotalVentas => Dinero.Redondear(
        VentasEfectivo + VentasTarjeta + VentasTransferencia + VentasCredito +
        VentasNequi + VentasDaviplata);

    public decimal VentasOtrosMedios => Dinero.Redondear(
        VentasTarjeta + VentasTransferencia + VentasCredito + VentasNequi + VentasDaviplata);

    /// <summary>
    /// Efectivo teórico en el cajón: base + ventas en efectivo + ingresos − egresos − devoluciones.
    /// </summary>
    public decimal MontoEsperado =>
        Dinero.Redondear(MontoInicial + VentasEfectivo + Ingresos - Egresos - Devoluciones);
}

/// <summary>Fila del historial de sesiones de caja.</summary>
public class CajaSesionDto
{
    public int Id { get; init; }

    public DateTime FechaApertura { get; init; }

    public DateTime? FechaCierre { get; init; }

    public string UsuarioApertura { get; init; } = string.Empty;

    public string UsuarioCierre { get; init; } = string.Empty;

    public decimal MontoInicial { get; init; }

    public decimal MontoEsperado { get; init; }

    public decimal MontoReal { get; init; }

    public decimal Diferencia { get; init; }

    public decimal TotalVentasEfectivo { get; init; }

    public decimal TotalVentasOtros { get; init; }

    public decimal TotalIngresos { get; init; }

    public decimal TotalEgresos { get; init; }

    public int CantidadVentas { get; init; }

    public EstadoCajaSesion Estado { get; init; }

    public string? ObservacionesApertura { get; init; }

    public string? ObservacionesCierre { get; init; }

    public bool EstaAbierta => Estado == EstadoCajaSesion.Abierta;

    public string EstadoTexto => Estado.Descripcion();

    /// <summary>Faltante, sobrante o cuadre exacto, para mostrar con color en la grilla.</summary>
    public string DiferenciaTexto => Diferencia switch
    {
        0 => "Cuadra exacto",
        < 0 => "Faltante",
        _ => "Sobrante"
    };

    public TimeSpan? Duracion => FechaCierre.HasValue ? FechaCierre.Value - FechaApertura : null;
}

/// <summary>Fila del detalle de movimientos de una sesión de caja.</summary>
public class MovimientoCajaDto
{
    public int Id { get; init; }

    public DateTime Fecha { get; init; }

    public TipoMovimientoCaja Tipo { get; init; }

    public decimal Monto { get; init; }

    public string Concepto { get; init; } = string.Empty;

    public string UsuarioNombre { get; init; } = string.Empty;

    public string? NumeroFactura { get; init; }

    public bool AfectaEfectivo { get; init; }

    public string TipoTexto => Tipo.Descripcion();

    /// <summary>Signo con el que el movimiento entra al arqueo.</summary>
    public int Signo => Tipo switch
    {
        TipoMovimientoCaja.Egreso => -1,
        TipoMovimientoCaja.AnulacionVenta => -1,
        _ => 1
    };

    public decimal MontoConSigno => Monto * Signo;
}
