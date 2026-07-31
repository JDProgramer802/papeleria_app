namespace Papeleria.Data.Storage;

/// <summary>
/// Resuelve y crea las carpetas de datos de la aplicación. Todo vive bajo
/// <c>%LOCALAPPDATA%\PapeleriaApp</c> para que el programa funcione sin permisos
/// de administrador y sin depender de la ruta del ejecutable.
/// </summary>
public static class RutasAplicacion
{
    public const string NombreCarpeta = "PapeleriaApp";
    public const string NombreBaseDatos = "papeleria.db";

    /// <summary>Carpeta raíz de datos de la aplicación.</summary>
    public static string Raiz { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        NombreCarpeta);

    public static string CarpetaDatos => Path.Combine(Raiz, "Data");

    public static string CarpetaImagenes => Path.Combine(Raiz, "Images");

    public static string CarpetaLogs => Path.Combine(Raiz, "Logs");

    public static string CarpetaBackupsPorDefecto => Path.Combine(Raiz, "Backups");

    public static string CarpetaExportaciones => Path.Combine(Raiz, "Exportaciones");

    public static string CarpetaTemporal => Path.Combine(Raiz, "Temp");

    /// <summary>Ruta completa del archivo SQLite.</summary>
    public static string ArchivoBaseDatos => Path.Combine(CarpetaDatos, NombreBaseDatos);

    /// <summary>Cadena de conexión SQLite con claves foráneas activas.</summary>
    public static string CadenaConexion =>
        $"Data Source={ArchivoBaseDatos};Foreign Keys=True;Pooling=True;Cache=Shared";

    /// <summary>Crea todas las carpetas necesarias si aún no existen. Idempotente.</summary>
    public static void AsegurarCarpetas()
    {
        foreach (var carpeta in new[]
                 {
                     Raiz, CarpetaDatos, CarpetaImagenes, CarpetaLogs,
                     CarpetaBackupsPorDefecto, CarpetaExportaciones, CarpetaTemporal
                 })
        {
            Directory.CreateDirectory(carpeta);
        }
    }

    /// <summary>Devuelve una ruta única dentro de la carpeta temporal.</summary>
    public static string RutaTemporal(string extension)
    {
        Directory.CreateDirectory(CarpetaTemporal);
        var nombre = $"{Guid.NewGuid():N}{(extension.StartsWith('.') ? extension : "." + extension)}";
        return Path.Combine(CarpetaTemporal, nombre);
    }
}
