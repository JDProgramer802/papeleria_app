using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Papeleria.Business.Security;
using Papeleria.Data.Storage;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Exceptions;

namespace Papeleria.Business.Services;

/// <inheritdoc cref="IServicioBackup" />
public class ServicioBackup : IServicioBackup
{
    private const string Extension = ".db";
    private const string PrefijoArchivo = "papeleria_backup_";

    private readonly IServicioConfiguracion _configuracion;
    private readonly IContextoSesion _sesion;
    private readonly ILogger<ServicioBackup> _log;

    public ServicioBackup(
        IServicioConfiguracion configuracion,
        IContextoSesion sesion,
        ILogger<ServicioBackup> log)
    {
        _configuracion = configuracion;
        _sesion = sesion;
        _log = log;
    }

    public string ObtenerCarpetaDestino()
    {
        var configurada = _configuracion.ObtenerTexto(ClavesConfiguracion.BackupCarpeta);

        return string.IsNullOrWhiteSpace(configurada)
            ? RutasAplicacion.CarpetaBackupsPorDefecto
            : configurada;
    }

    public EstadoRespaldoDto ObtenerEstado() => new()
    {
        UltimaCopia = _configuracion.ObtenerFecha(ClavesConfiguracion.BackupUltimaFecha),
        Carpeta = ObtenerCarpetaDestino(),
        Automatico = _configuracion.ObtenerBooleano(ClavesConfiguracion.BackupAutomatico, true),
        FrecuenciaDias = Math.Max(
            _configuracion.ObtenerEntero(ClavesConfiguracion.BackupFrecuenciaDias, 1), 1)
    };

    public async Task<string> CrearAsync(string? carpetaDestino = null, CancellationToken ct = default)
    {
        var carpeta = string.IsNullOrWhiteSpace(carpetaDestino) ? ObtenerCarpetaDestino() : carpetaDestino;

        try
        {
            Directory.CreateDirectory(carpeta);
        }
        catch (Exception ex)
        {
            throw new NegocioException(
                $"No se pudo acceder a la carpeta de respaldos «{carpeta}». {ex.Message}", ex);
        }

        if (!File.Exists(RutasAplicacion.ArchivoBaseDatos))
        {
            throw new NegocioException("Todavía no existe una base de datos que respaldar.");
        }

        var nombre = $"{PrefijoArchivo}{DateTime.Now:yyyyMMdd_HHmmss}{Extension}";
        var destino = Path.Combine(carpeta, nombre);

        try
        {
            // La API de respaldo de SQLite copia la base de forma consistente aunque
            // haya operaciones en curso, sin necesidad de cerrar la aplicación.
            await Task.Run(() =>
            {
                using var origen = new SqliteConnection(RutasAplicacion.CadenaConexion);
                using var copia = new SqliteConnection($"Data Source={destino}");

                origen.Open();
                copia.Open();
                origen.BackupDatabase(copia);
            }, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new NegocioException($"No se pudo crear la copia de seguridad. {ex.Message}", ex);
        }

        // Una copia que no se puede abrir es peor que ninguna: da tranquilidad falsa.
        // Se comprueba aquí mismo y, si salió mal, se borra y se avisa en el momento,
        // no el día que haga falta restaurarla.
        try
        {
            await VerificarQueEsBaseDeDatosAsync(destino, ct).ConfigureAwait(false);
        }
        catch (NegocioException)
        {
            TryEliminar(destino);

            throw new NegocioException(
                $"La copia se escribió en «{carpeta}» pero quedó dañada y se descartó. " +
                "Revise que el destino tenga espacio y siga conectado.");
        }

        await _configuracion.GuardarAsync(
            ClavesConfiguracion.BackupUltimaFecha,
            DateTime.Now.ToString("O", CultureInfo.InvariantCulture), ct).ConfigureAwait(false);

        _log.LogInformation("Copia de seguridad creada en {Ruta} por {Usuario}",
            destino, _sesion.Usuario?.NombreUsuario ?? "sistema");

        await DepurarAntiguasAsync(ct).ConfigureAwait(false);

        return destino;
    }

    public async Task RestaurarAsync(string archivoBackup, CancellationToken ct = default)
    {
        if (!_sesion.EsAdministrador)
        {
            throw new PermisoDenegadoException(
                "Solo un administrador puede restaurar una copia de seguridad.");
        }

        if (string.IsNullOrWhiteSpace(archivoBackup) || !File.Exists(archivoBackup))
        {
            throw new NegocioException("El archivo de respaldo seleccionado no existe.");
        }

        await VerificarQueEsBaseDeDatosAsync(archivoBackup, ct).ConfigureAwait(false);

        // Antes de sobrescribir se conserva el estado actual: si el respaldo estuviera
        // dañado, el negocio no se queda sin datos.
        var copiaSeguridad = Path.Combine(
            RutasAplicacion.CarpetaBackupsPorDefecto,
            $"previo_a_restaurar_{DateTime.Now:yyyyMMdd_HHmmss}{Extension}");

        try
        {
            Directory.CreateDirectory(RutasAplicacion.CarpetaBackupsPorDefecto);

            if (File.Exists(RutasAplicacion.ArchivoBaseDatos))
            {
                await CrearAsync(RutasAplicacion.CarpetaBackupsPorDefecto, ct).ConfigureAwait(false);
                File.Copy(RutasAplicacion.ArchivoBaseDatos, copiaSeguridad, overwrite: true);
            }

            // Las conexiones agrupadas mantienen el archivo abierto: hay que liberarlas.
            SqliteConnection.ClearAllPools();
            await Task.Delay(200, ct).ConfigureAwait(false);

            EliminarArchivosAuxiliares();

            File.Copy(archivoBackup, RutasAplicacion.ArchivoBaseDatos, overwrite: true);
        }
        catch (Exception ex) when (ex is not NegocioException)
        {
            throw new NegocioException(
                $"No se pudo restaurar la copia de seguridad. {ex.Message} " +
                (File.Exists(copiaSeguridad)
                    ? $"La base anterior quedó guardada en «{copiaSeguridad}»."
                    : string.Empty), ex);
        }

        _log.LogWarning("Base de datos restaurada desde {Ruta} por {Usuario}",
            archivoBackup, _sesion.Usuario?.NombreUsuario);
    }

    /// <summary>Comprueba la cabecera del archivo para no restaurar algo que no sea SQLite.</summary>
    private static void TryEliminar(string archivo)
    {
        try
        {
            if (File.Exists(archivo))
            {
                File.Delete(archivo);
            }
        }
        catch
        {
            // Si el archivo dañado no se deja borrar, tampoco pasa nada: no se anota
            // como copia buena y el aviso de respaldo atrasado seguirá encendido.
        }
    }

    private static async Task VerificarQueEsBaseDeDatosAsync(string archivo, CancellationToken ct)
    {
        const string cabeceraEsperada = "SQLite format 3";

        try
        {
            await using var flujo = File.OpenRead(archivo);
            var buffer = new byte[16];
            var leidos = await flujo.ReadAsync(buffer, ct).ConfigureAwait(false);

            var cabecera = System.Text.Encoding.ASCII.GetString(buffer, 0, Math.Max(leidos - 1, 0));

            if (leidos < 16 || !cabecera.StartsWith(cabeceraEsperada, StringComparison.Ordinal))
            {
                throw new NegocioException(
                    "El archivo seleccionado no es una base de datos válida del sistema.");
            }
        }
        catch (Exception ex) when (ex is not NegocioException)
        {
            throw new NegocioException($"No se pudo leer el archivo de respaldo. {ex.Message}", ex);
        }
    }

    /// <summary>
    /// En modo WAL, SQLite mantiene los archivos <c>-wal</c> y <c>-shm</c> junto a la base.
    /// Al restaurar deben eliminarse o el motor mezclaría transacciones de la base anterior.
    /// </summary>
    private static void EliminarArchivosAuxiliares()
    {
        foreach (var sufijo in new[] { "-wal", "-shm" })
        {
            var auxiliar = RutasAplicacion.ArchivoBaseDatos + sufijo;

            if (File.Exists(auxiliar))
            {
                try
                {
                    File.Delete(auxiliar);
                }
                catch (IOException)
                {
                    // Si sigue bloqueado, la copia posterior lo dejará coherente igualmente.
                }
            }
        }
    }

    public Task<List<ArchivoBackupDto>> ListarAsync(string? carpeta = null, CancellationToken ct = default)
    {
        var destino = string.IsNullOrWhiteSpace(carpeta) ? ObtenerCarpetaDestino() : carpeta;

        if (!Directory.Exists(destino))
        {
            return Task.FromResult(new List<ArchivoBackupDto>());
        }

        var archivos = new DirectoryInfo(destino)
            .GetFiles($"*{Extension}")
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => new ArchivoBackupDto
            {
                Ruta = f.FullName,
                Nombre = f.Name,
                Fecha = f.LastWriteTime,
                TamanoBytes = f.Length
            })
            .ToList();

        return Task.FromResult(archivos);
    }

    public async Task<string?> EjecutarProgramadoAsync(CancellationToken ct = default)
    {
        if (!_configuracion.ObtenerBooleano(ClavesConfiguracion.BackupAutomatico, true))
        {
            return null;
        }

        var frecuencia = Math.Max(_configuracion.ObtenerEntero(ClavesConfiguracion.BackupFrecuenciaDias, 1), 1);
        var ultima = _configuracion.ObtenerFecha(ClavesConfiguracion.BackupUltimaFecha);

        if (ultima is { } fecha && (DateTime.Now - fecha).TotalDays < frecuencia)
        {
            return null;
        }

        try
        {
            return await CrearAsync(ct: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Un fallo de respaldo automático no debe impedir usar el programa.
            _log.LogError(ex, "No se pudo completar la copia de seguridad automática");
            return null;
        }
    }

    public async Task<int> DepurarAntiguasAsync(CancellationToken ct = default)
    {
        var conservar = _configuracion.ObtenerEntero(ClavesConfiguracion.BackupRetencion, 30);

        if (conservar <= 0)
        {
            return 0;
        }

        var archivos = await ListarAsync(ct: ct).ConfigureAwait(false);

        if (archivos.Count <= conservar)
        {
            return 0;
        }

        var eliminados = 0;

        foreach (var archivo in archivos.Skip(conservar))
        {
            try
            {
                File.Delete(archivo.Ruta);
                eliminados++;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "No se pudo eliminar la copia antigua {Ruta}", archivo.Ruta);
            }
        }

        if (eliminados > 0)
        {
            _log.LogInformation("Se eliminaron {Cantidad} copias de seguridad antiguas", eliminados);
        }

        return eliminados;
    }
}
