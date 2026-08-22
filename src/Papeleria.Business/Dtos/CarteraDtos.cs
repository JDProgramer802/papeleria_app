using Papeleria.Business.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Business.Dtos;

/// <summary>Deuda acumulada de un cliente, tal como se lista en el módulo de cartera.</summary>
public class SaldoClienteDto
{
    public int ClienteId { get; init; }

    public string Nombre { get; init; } = string.Empty;

    public string? NumeroDocumento { get; init; }

    public string? Telefono { get; init; }

    /// <summary>Suma de las facturas a crédito vigentes.</summary>
    public decimal TotalFiado { get; init; }

    public decimal TotalAbonado { get; init; }

    public decimal LimiteCredito { get; init; }

    public int FacturasPendientes { get; init; }

    /// <summary>Fecha de la factura pendiente más antigua.</summary>
    public DateTime? DeudaMasAntigua { get; init; }

    public decimal Saldo => Dinero.Redondear(TotalFiado - TotalAbonado);

    /// <summary>Lo que todavía se le puede fiar sin pasarse del cupo.</summary>
    public decimal CupoDisponible => Dinero.Redondear(Math.Max(LimiteCredito - Saldo, 0));

    public bool SuperaCupo => LimiteCredito > 0 && Saldo > LimiteCredito;

    /// <summary>Días transcurridos desde la factura pendiente más antigua.</summary>
    public int DiasDeMora =>
        DeudaMasAntigua is { } fecha ? Math.Max((DateTime.Today - fecha.Date).Days, 0) : 0;
}

/// <summary>Factura a crédito con lo que queda por cobrar de ella.</summary>
public class FacturaCreditoDto
{
    public int VentaId { get; init; }

    public string NumeroFactura { get; init; } = string.Empty;

    public DateTime Fecha { get; init; }

    public decimal Total { get; init; }

    /// <summary>Parte cubierta por los abonos, aplicados de la factura más antigua a la más nueva.</summary>
    public decimal Aplicado { get; set; }

    public decimal Pendiente => Dinero.Redondear(Total - Aplicado);

    public bool EstaSaldada => Pendiente <= 0;

    public int Dias => Math.Max((DateTime.Today - Fecha.Date).Days, 0);
}

/// <summary>Abono recibido de un cliente.</summary>
public class AbonoDto
{
    public int Id { get; init; }

    public int ClienteId { get; init; }

    public string ClienteNombre { get; init; } = string.Empty;

    public DateTime Fecha { get; init; }

    public decimal Monto { get; init; }

    public MetodoPago MetodoPago { get; init; }

    public string UsuarioNombre { get; init; } = string.Empty;

    public string? Observaciones { get; init; }

    public bool Anulado { get; init; }

    public string? MotivoAnulacion { get; init; }
}

/// <summary>Estado de cuenta completo de un cliente.</summary>
public class EstadoCuentaDto
{
    public SaldoClienteDto Resumen { get; init; } = new();

    public IReadOnlyList<FacturaCreditoDto> Facturas { get; init; } = Array.Empty<FacturaCreditoDto>();

    public IReadOnlyList<AbonoDto> Abonos { get; init; } = Array.Empty<AbonoDto>();

    public bool TieneMovimientos => Facturas.Count > 0 || Abonos.Count > 0;
}

/// <summary>Datos con los que se registra el pago de un cliente.</summary>
public class SolicitudAbono
{
    public int ClienteId { get; set; }

    public decimal Monto { get; set; }

    public MetodoPago MetodoPago { get; set; } = MetodoPago.Efectivo;

    public string? Observaciones { get; set; }
}

/// <summary>Criterios de consulta de la cartera.</summary>
public class FiltroCartera
{
    public string? Texto { get; set; }

    /// <summary>Oculta a los clientes que ya no deben nada.</summary>
    public bool SoloConSaldo { get; set; } = true;

    /// <summary>Deja solo a quienes llevan más de estos días sin pagar.</summary>
    public int? DiasMoraMinimos { get; set; }

    public int Pagina { get; set; } = 1;

    public int TamanoPagina { get; set; } = 25;
}

/// <summary>Cifras del conjunto de la cartera.</summary>
public class ResumenCarteraDto
{
    public int ClientesConDeuda { get; init; }

    public decimal SaldoTotal { get; init; }

    public decimal VencidoA30 { get; init; }

    public decimal VencidoA60 { get; init; }

    public decimal VencidoMas60 { get; init; }

    public bool HayVencido => VencidoA30 + VencidoA60 + VencidoMas60 > 0;
}
