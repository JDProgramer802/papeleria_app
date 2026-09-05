using Papeleria.Business.Dtos;

namespace Papeleria.Business.Services;

/// <summary>
/// Carga del catálogo desde una hoja de cálculo.
///
/// Una papelería que estrena el programa llega con mil o dos mil referencias ya
/// existiendo en algún lado —una hoja de Excel, el listado del proveedor—. Teclearlas
/// una por una no es una opción, y sin esto el montaje inicial se vuelve el motivo
/// para no usar el programa.
///
/// Sirve igual para subir precios en bloque: se exporta, se ajusta la columna del
/// precio y se vuelve a cargar.
/// </summary>
public interface IServicioImportacion
{
    /// <summary>
    /// Lee el archivo y devuelve lo que encontró, sin tocar la base de datos. Cada
    /// fila viene con lo que se va a hacer con ella y con el motivo si no se puede.
    /// </summary>
    Task<PrevisualizacionImportacion> PrevisualizarAsync(string archivo, CancellationToken ct = default);

    /// <summary>Aplica las filas válidas: crea las nuevas y actualiza las que ya existen.</summary>
    Task<ResultadoImportacion> ImportarAsync(
        PrevisualizacionImportacion previsualizacion, CancellationToken ct = default);

    /// <summary>
    /// Genera una plantilla con las columnas esperadas y un par de ejemplos. Sin esto,
    /// el usuario tiene que adivinar los encabezados y la primera carga siempre falla.
    /// </summary>
    Task<string> GenerarPlantillaAsync(string? rutaDestino = null, CancellationToken ct = default);
}
