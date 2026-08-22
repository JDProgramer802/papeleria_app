using Papeleria.Business.Dtos;
using Papeleria.Domain.Common;

namespace Papeleria.Business.Services;

/// <summary>
/// Cuentas por cobrar. Una venta a crédito deja una deuda a nombre del cliente y aquí
/// se consulta, se abona y se controla el cupo. Los abonos se aplican a la cuenta y el
/// sistema los reparte entre las facturas de la más antigua a la más reciente.
/// </summary>
public interface IServicioCartera
{
    Task<ResultadoPaginado<SaldoClienteDto>> BuscarAsync(
        FiltroCartera filtro, CancellationToken ct = default);

    /// <summary>Cifras de toda la cartera, con el vencido repartido por antigüedad.</summary>
    Task<ResumenCarteraDto> ObtenerResumenAsync(FiltroCartera filtro, CancellationToken ct = default);

    /// <summary>Facturas a crédito y abonos de un cliente, con lo pendiente de cada factura.</summary>
    Task<EstadoCuentaDto> ObtenerEstadoCuentaAsync(int clienteId, CancellationToken ct = default);

    /// <summary>Deuda vigente de un cliente. La usa el punto de venta antes de fiar.</summary>
    Task<SaldoClienteDto> ObtenerSaldoAsync(int clienteId, CancellationToken ct = default);

    /// <summary>
    /// Registra un pago del cliente. Si entra en efectivo exige caja abierta y suma al cajón.
    /// </summary>
    Task<AbonoDto> RegistrarAbonoAsync(SolicitudAbono solicitud, CancellationToken ct = default);

    /// <summary>Anula un abono mal registrado devolviendo la deuda. Reservado al administrador.</summary>
    Task AnularAbonoAsync(int abonoId, string motivo, CancellationToken ct = default);
}
