using Papeleria.Business.Dtos;
using Papeleria.Domain.Common;

namespace Papeleria.Business.Services;

/// <summary>Criterios de consulta del historial de compras.</summary>
public class FiltroCompras
{
    public string? Texto { get; set; }

    public int? ProveedorId { get; set; }

    public DateTime? Desde { get; set; }

    public DateTime? Hasta { get; set; }

    public bool IncluirAnuladas { get; set; } = true;

    public int Pagina { get; set; } = 1;

    public int TamanoPagina { get; set; } = 25;
}

/// <summary>
/// Registro de compras a proveedores. Guardar una compra incrementa existencias,
/// recalcula el costo promedio y deja rastro en el kardex, todo en una transacción.
/// </summary>
public interface IServicioCompras
{
    Task<ResultadoPaginado<CompraResumenDto>> BuscarAsync(FiltroCompras filtro, CancellationToken ct = default);

    Task<CompraDetalladaDto?> ObtenerDetalleAsync(int compraId, CancellationToken ct = default);

    /// <summary>Registra la compra y devuelve su identificador y número consecutivo.</summary>
    Task<CompraResumenDto> RegistrarAsync(SolicitudCompra solicitud, CancellationToken ct = default);

    /// <summary>Anula la compra devolviendo la mercancía al kardex. Requiere existencias suficientes.</summary>
    Task AnularAsync(int compraId, string motivo, CancellationToken ct = default);
}
