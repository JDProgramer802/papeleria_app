using Papeleria.Business.Dtos;

namespace Papeleria.Business.Services;

/// <summary>
/// Acompaña la puesta en marcha. En lugar de una presentación de diapositivas, el
/// tutorial mira el estado real del negocio y dice qué falta por dejar listo.
/// </summary>
public interface IServicioAyuda
{
    /// <summary>Pasos de la puesta en marcha, cada uno con si ya está resuelto.</summary>
    Task<ProgresoTutorialDto> ObtenerProgresoAsync(CancellationToken ct = default);
}
