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
/// Estado de los respaldos, para poder avisar a tiempo.
///
/// La copia automática se hace sola y en silencio. Si el destino es una memoria que
/// hoy no está conectada, falla, queda anotado en el registro y nadie se entera hasta
/// el día que hace falta. Con esto el panel puede decirlo en voz alta.
/// </summary>
public class EstadoRespaldoDto
{
    public DateTime? UltimaCopia { get; init; }

    public required string Carpeta { get; init; }

    public bool Automatico { get; init; }

    /// <summary>Cada cuántos días debería hacerse una copia.</summary>
    public int FrecuenciaDias { get; init; }

    public bool NuncaSeHaHecho => UltimaCopia is null;

    public int DiasDesdeLaUltima => UltimaCopia is { } fecha
        ? Math.Max((int)(DateTime.Now.Date - fecha.Date).TotalDays, 0)
        : int.MaxValue;

    /// <summary>
    /// Se da por atrasada al doblar la frecuencia pactada: un día de retraso puede ser
    /// que el computador estuviera apagado; el doble ya es que algo no está funcionando.
    /// </summary>
    public bool Atrasado => NuncaSeHaHecho || DiasDesdeLaUltima > Math.Max(FrecuenciaDias, 1) * 2;

    public string Resumen => UltimaCopia is { } fecha
        ? DiasDesdeLaUltima switch
        {
            0 => $"Última copia hoy a las {fecha:HH:mm}",
            1 => "Última copia ayer",
            _ => $"Última copia hace {DiasDesdeLaUltima} días"
        }
        : "Todavía no se ha hecho ninguna copia";
}

/// <summary>
/// Copias de seguridad de la base de datos SQLite. Usa la API de respaldo en línea
/// del motor, por lo que puede ejecutarse con la aplicación abierta.
/// </summary>
public interface IServicioBackup
{
    /// <summary>Carpeta configurada para las copias; si no hay ninguna, la de la aplicación.</summary>
    string ObtenerCarpetaDestino();

    /// <summary>Cuándo se hizo la última copia y si ya se pasó de la cuenta.</summary>
    EstadoRespaldoDto ObtenerEstado();

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
