namespace Papeleria.Business.Services;

/// <summary>Archivo de copia de seguridad encontrado en la carpeta de respaldos.</summary>
public class ArchivoBackupDto
{
    public required string Ruta { get; init; }

    public required string Nombre { get; init; }

    public required DateTime Fecha { get; init; }

    public required long TamanoBytes { get; init; }

    /// <summary>Tamaño legible para mostrar en la grilla (KB o MB).</summary>
    public string TamanoTexto => TamanoBytes switch
    {
        < 1024 => $"{TamanoBytes} B",
        < 1024 * 1024 => $"{TamanoBytes / 1024d:N1} KB",
        _ => $"{TamanoBytes / (1024d * 1024d):N1} MB"
    };
}

/// <summary>
/// Copias de seguridad de la base de datos SQLite. Usa la API de respaldo en línea
/// del motor, por lo que puede ejecutarse con la aplicación abierta.
/// </summary>
public interface IServicioBackup
{
    /// <summary>Carpeta configurada para las copias; si no hay ninguna, la de la aplicación.</summary>
    string ObtenerCarpetaDestino();

    Task<string> CrearAsync(string? carpetaDestino = null, CancellationToken ct = default);

    /// <summary>
    /// Sustituye la base de datos actual por la del respaldo. Antes guarda una copia
    /// de seguridad del estado vigente. Requiere reiniciar la aplicación.
    /// </summary>
    Task RestaurarAsync(string archivoBackup, CancellationToken ct = default);

    Task<List<ArchivoBackupDto>> ListarAsync(string? carpeta = null, CancellationToken ct = default);

    /// <summary>Genera la copia programada si ya pasó el intervalo configurado.</summary>
    Task<string?> EjecutarProgramadoAsync(CancellationToken ct = default);

    /// <summary>Elimina las copias más antiguas conservando las indicadas en la configuración.</summary>
    Task<int> DepurarAntiguasAsync(CancellationToken ct = default);
}
