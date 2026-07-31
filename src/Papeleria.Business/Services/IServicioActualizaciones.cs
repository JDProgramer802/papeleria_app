namespace Papeleria.Business.Services;

/// <summary>Versión publicada en GitHub que supera a la instalada.</summary>
public class ActualizacionDisponible
{
    public required Version Version { get; init; }

    public required string Nombre { get; init; }

    /// <summary>Notas de la versión escritas al publicar la release.</summary>
    public string Notas { get; init; } = string.Empty;

    public required string UrlDescarga { get; init; }

    public required long TamanoBytes { get; init; }

    /// <summary>Huella SHA-256 publicada por GitHub, cuando está disponible.</summary>
    public string? Sha256 { get; init; }

    public DateTime? Publicada { get; init; }

    public string TamanoTexto => $"{TamanoBytes / (1024d * 1024d):N0} MB";
}

/// <summary>Motivo por el que no se puede aplicar una actualización en este equipo.</summary>
public enum ImpedimentoActualizacion
{
    Ninguno = 0,

    /// <summary>Se está ejecutando la compilación de desarrollo, no el ejecutable publicado.</summary>
    NoEsEjecutablePublicado = 1,

    /// <summary>No hay permiso de escritura en la carpeta donde vive el ejecutable.</summary>
    CarpetaSoloLectura = 2
}

/// <summary>
/// Actualización de la aplicación a partir de las publicaciones («releases») de un
/// repositorio de GitHub. Toda la funcionalidad es opcional: si no hay conexión el
/// programa sigue trabajando con normalidad.
/// </summary>
public interface IServicioActualizaciones
{
    /// <summary>Versión que se está ejecutando.</summary>
    Version VersionActual { get; }

    /// <summary>Indica si este equipo puede aplicar actualizaciones y, si no, por qué.</summary>
    ImpedimentoActualizacion ComprobarViabilidad();

    /// <summary>
    /// Consulta la última publicación del repositorio. Devuelve <c>null</c> si ya se
    /// tiene la versión más reciente, si no hay conexión o si el repositorio no está
    /// configurado: nunca lanza por problemas de red.
    /// </summary>
    Task<ActualizacionDisponible?> ComprobarAsync(bool forzar = false, CancellationToken ct = default);

    /// <summary>Descarga el ejecutable nuevo y verifica su integridad.</summary>
    Task<string> DescargarAsync(
        ActualizacionDisponible actualizacion, IProgress<double>? progreso = null, CancellationToken ct = default);

    /// <summary>
    /// Sustituye el ejecutable en uso por el descargado. Tras completarse hay que
    /// reiniciar la aplicación para que tome efecto.
    /// </summary>
    Task AplicarAsync(string archivoDescargado, CancellationToken ct = default);

    /// <summary>Deja de avisar de esta versión concreta.</summary>
    Task OmitirVersionAsync(Version version, CancellationToken ct = default);

    /// <summary>Elimina el ejecutable anterior que quedó tras una actualización.</summary>
    void LimpiarRestosDeActualizacion();
}
