using Papeleria.Business.Dtos;
using Papeleria.Domain.Common;

namespace Papeleria.Business.Services;

/// <summary>Datos de un movimiento manual de inventario.</summary>
public class SolicitudMovimientoInventario
{
    public int ProductoId { get; set; }

    public decimal Cantidad { get; set; }

    public string Motivo { get; set; } = string.Empty;

    /// <summary>Documento o referencia externa que respalda el movimiento.</summary>
    public string? DocumentoReferencia { get; set; }

    /// <summary>Costo unitario a registrar; si es nulo se usa el costo actual del producto.</summary>
    public decimal? CostoUnitario { get; set; }
}

/// <summary>Datos de una transferencia de ubicación dentro de la bodega.</summary>
public class SolicitudTransferencia
{
    public int ProductoId { get; set; }

    public decimal Cantidad { get; set; }

    public string UbicacionOrigen { get; set; } = string.Empty;

    public string UbicacionDestino { get; set; } = string.Empty;

    public string? Observaciones { get; set; }
}

/// <summary>Resumen del valor y la composición del inventario.</summary>
public class ResumenInventarioDto
{
    public int TotalProductos { get; init; }

    public int ProductosActivos { get; init; }

    public decimal UnidadesTotales { get; init; }

    public decimal ValorCosto { get; init; }

    public decimal ValorVenta { get; init; }

    public int Agotados { get; init; }

    public int BajoMinimo { get; init; }

    public int SobreMaximo { get; init; }

    /// <summary>Utilidad potencial si se vendiera todo el inventario.</summary>
    public decimal UtilidadPotencial => ValorVenta - ValorCosto;
}

/// <summary>
/// Movimientos manuales de existencias. Todos pasan por el kardex: no existe
/// ninguna vía para alterar el stock sin dejar rastro.
/// </summary>
public interface IServicioInventario
{
    Task<ResultadoPaginado<ProductoListadoDto>> ConsultarExistenciasAsync(
        FiltroProductos filtro, CancellationToken ct = default);

    Task<ResumenInventarioDto> ObtenerResumenAsync(CancellationToken ct = default);

    Task RegistrarEntradaAsync(SolicitudMovimientoInventario solicitud, CancellationToken ct = default);

    Task RegistrarSalidaAsync(SolicitudMovimientoInventario solicitud, CancellationToken ct = default);

    /// <summary>
    /// Ajusta el inventario dejando el producto en la cantidad indicada. Genera un
    /// movimiento positivo o negativo según la diferencia contra el stock actual.
    /// </summary>
    Task RegistrarAjusteAsync(int productoId, decimal stockReal, string motivo, CancellationToken ct = default);

    Task RegistrarTransferenciaAsync(SolicitudTransferencia solicitud, CancellationToken ct = default);
}
