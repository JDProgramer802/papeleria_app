using Papeleria.Business.Dtos;
using Papeleria.Data.Repositories;

namespace Papeleria.Business.Services;

/// <summary>
/// Acceso tipado al almacén clave/valor de configuración, con caché en memoria para
/// que consultarlo desde la interfaz no golpee la base de datos en cada enlace.
/// </summary>
public interface IServicioConfiguracion
{
    /// <summary>Carga (o recarga) toda la configuración en memoria.</summary>
    Task CargarAsync(CancellationToken ct = default);

    string ObtenerTexto(string clave, string valorPorDefecto = "");

    int ObtenerEntero(string clave, int valorPorDefecto = 0);

    decimal ObtenerDecimal(string clave, decimal valorPorDefecto = 0);

    bool ObtenerBooleano(string clave, bool valorPorDefecto = false);

    DateTime? ObtenerFecha(string clave);

    Task GuardarAsync(string clave, string? valor, CancellationToken ct = default);

    Task GuardarVariosAsync(IReadOnlyDictionary<string, string?> valores, CancellationToken ct = default);

    /// <summary>Instantánea de los datos de empresa según la caché actual.</summary>
    DatosEmpresa ObtenerEmpresa();

    Task GuardarEmpresaAsync(DatosEmpresa empresa, CancellationToken ct = default);

    /// <summary>
    /// Reserva el siguiente número de un consecutivo dentro de la transacción indicada,
    /// de forma que factura y numeración se confirmen o se reviertan juntas.
    /// </summary>
    Task<string> ReservarConsecutivoAsync(
        IUnidadDeTrabajo unidad, string clavePrefijo, string claveConsecutivo, CancellationToken ct = default);

    /// <summary>Se dispara tras guardar cambios, para que la interfaz se refresque.</summary>
    event EventHandler? ConfiguracionCambiada;
}
