using Papeleria.Business.Dtos;
using Papeleria.Data.Repositories;
using Papeleria.Domain.Entities;

namespace Papeleria.Business.Services;

/// <summary>
/// Control del turno de caja: apertura con base, ingresos y egresos, arqueo y cierre.
/// El punto de venta exige una sesión abierta para poder facturar.
/// </summary>
public interface IServicioCaja
{
    /// <summary>Sesión abierta actualmente, o <c>null</c> si la caja está cerrada.</summary>
    Task<CajaSesion?> ObtenerSesionAbiertaAsync(CancellationToken ct = default);

    Task<bool> HayCajaAbiertaAsync(CancellationToken ct = default);

    Task<CajaSesion> AbrirAsync(decimal montoInicial, string? observaciones, CancellationToken ct = default);

    Task<MovimientoCaja> RegistrarIngresoAsync(decimal monto, string concepto, CancellationToken ct = default);

    Task<MovimientoCaja> RegistrarEgresoAsync(decimal monto, string concepto, CancellationToken ct = default);

    /// <summary>Recalcula el efectivo esperado a partir de los movimientos de la sesión.</summary>
    Task<ArqueoCajaDto> CalcularArqueoAsync(int cajaSesionId, CancellationToken ct = default);

    Task<CajaSesion> CerrarAsync(
        int cajaSesionId, decimal montoReal, string? observaciones, CancellationToken ct = default);

    Task<List<CajaSesionDto>> ListarSesionesAsync(
        DateTime? desde, DateTime? hasta, CancellationToken ct = default);

    Task<CajaSesionDto?> ObtenerSesionAsync(int cajaSesionId, CancellationToken ct = default);

    Task<List<MovimientoCajaDto>> ObtenerMovimientosAsync(int cajaSesionId, CancellationToken ct = default);

    /// <summary>
    /// Registra en caja el movimiento generado por una venta. Se llama desde la
    /// transacción del punto de venta, nunca de forma aislada.
    /// </summary>
    Task RegistrarMovimientoDeVentaAsync(
        IUnidadDeTrabajo unidad, Venta venta, int usuarioId, CancellationToken ct = default);

    /// <summary>Contrapartida en caja cuando se anula una venta ya cobrada.</summary>
    Task RegistrarAnulacionDeVentaAsync(
        IUnidadDeTrabajo unidad, Venta venta, int usuarioId, CancellationToken ct = default);
}
