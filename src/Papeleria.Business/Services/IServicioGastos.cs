using Papeleria.Business.Dtos;

namespace Papeleria.Business.Services;

public interface IServicioGastos
{
    Task<int> RegistrarAsync(SolicitudGasto solicitud, CancellationToken ct = default);
    Task AnularAsync(int id, string motivo, CancellationToken ct = default);
    Task<IReadOnlyList<GastoDto>> BuscarAsync(FiltroGastos filtro, CancellationToken ct = default);
}
