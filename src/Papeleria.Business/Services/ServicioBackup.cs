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

    /// <summary>
    /// Mientras se escribe, la copia lleva este sufijo. Solo se le quita cuando ya se
    /// comprobó que sirve, y quitarlo es un renombrado, que en Windows es atómico: o hay
    /// copia entera o no hay archivo. Nunca un archivo a medias con nombre de bueno,
    /// esperando al día que alguien lo necesite de verdad.
    /// </summary>
    private const string SufijoParcial = ".parcial";

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
        var provisional = destino + SufijoParcial;

        TryEliminar(provisional);

        try
        {
            // La API de respaldo de SQLite copia la base de forma consistente aunque
            // haya operaciones en curso, sin necesidad de cerrar la aplicación.
            await Task.Run(() => CopiarBaseDatos(provisional), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TryEliminar(provisional);

            throw new NegocioException($"No se pudo crear la copia de seguridad. {ex.Message}", ex);
        }

        // Una copia que no se puede abrir es peor que ninguna: da tranquilidad falsa.
        // Se comprueba aquí mismo y, si salió mal, se borra y se avisa en el momento,
        // no el día que haga falta restaurarla.
        try
        {
            await VerificarQueEsBaseDeDatosAsync(provisional, ct).ConfigureAwait(false);
        }
        catch (NegocioException ex)
        {
            TryEliminar(provisional);

            // El motivo de verdad va en el mensaje. Cuando este aviso decía solo
            // «quedó dañada», el usuario no tenía forma de saber si era el disco, el
            // destino desconectado o un fallo del programa, y resultó ser lo tercero.
            throw new NegocioException(
                $"La copia se escribió en «{carpeta}» pero no se pudo dar por buena y se " +
                $"descartó. {ex.Message}", ex);
        }

        try
        {
            File.Move(provisional, destino, overwrite: true);
        }
        catch (Exception ex)
        {
            TryEliminar(provisional);

            throw new NegocioException(
                $"La copia se hizo bien pero no se pudo dejar en «{carpeta}». {ex.Message}", ex);
        }

        await _configuracion.GuardarAsync(
            ClavesConfiguracion.BackupUltimaFecha,
            DateTime.Now.ToString("O", CultureInfo.InvariantCulture), ct).ConfigureAwait(false);

        _log.LogInformation("Copia de seguridad creada en {Ruta} por {Usuario}",
            destino, _sesion.Usuario?.NombreUsuario ?? "sistema");

        await DepurarAntiguasAsync(ct).ConfigureAwait(false);

        return destino;
    }

    /// <summary>
    /// Vuelca la base de datos en un archivo nuevo con la API de respaldo del motor.
    ///
    /// El «Pooling=False» del destino no es un adorno. Con el pool activado —que es lo
    /// que trae de fábrica— cerrar la conexión de destino la devuelve al pool con el
    /// archivo TODAVÍA abierto: la comprobación posterior no podía ni leerlo, una copia
    /// perfectamente buena se daba por dañada, y el intento de borrarla también fallaba,
    /// así que la carpeta se iba llenando de respaldos «rotos» que en realidad servían.
    /// </summary>
    private static void CopiarBaseDatos(string destino)
    {
        using var origen = new SqliteConnection(RutasAplicacion.CadenaConexion);
        using var copia = new SqliteConnection($"Data Source={destino};Pooling=False");

        origen.Open();
        copia.Open();

        origen.BackupDatabase(copia);

        copia.Close();
        origen.Close();
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
        // dañado, el negocio no se queda sin datos. Se hace con la API del motor y no
        // con File.Copy: en modo WAL, copiar el archivo a pelo puede dejar fuera las
        // últimas transacciones, y esta es justamente la copia de la que depende el
        // rescate si la restauración sale mal.
        var copiaSeguridad = string.Empty;

        try
        {
            Directory.CreateDirectory(RutasAplicacion.CarpetaBackupsPorDefecto);

            if (File.Exists(RutasAplicacion.ArchivoBaseDatos))
            {
                copiaSeguridad = await CrearAsync(RutasAplicacion.CarpetaBackupsPorDefecto, ct)
                    .ConfigureAwait(false);
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
                (copiaSeguridad.Length > 0 && File.Exists(copiaSeguridad)
                    ? $"La base anterior quedó guardada en «{copiaSeguridad}»."
                    : string.Empty), ex);
        }

        _log.LogWarning("Base de datos restaurada desde {Ruta} por {Usuario}",
            archivoBackup, _sesion.Usuario?.NombreUsuario);
    }

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

    /// <summary>Tablas que tiene que traer cualquier copia que sea de este sistema.</summary>
    private static readonly string[] TablasEsperadas =
    {
        "Ventas", "VentaDetalles", "Productos", "Clientes", "Configuraciones", "MovimientosKardex"
    };

    /// <summary>
    /// Comprueba que el archivo es una copia utilizable de ESTA base de datos.
    ///
    /// Mirar los primeros bytes no bastaba, y era peligroso: SQLite escribe la cabecera
    /// al principio del respaldo, de modo que una copia cortada por la mitad la tiene
    /// perfecta y pasaba por buena. La única forma de saber si una copia sirve es
    /// abrirla y hacerla hablar: que el motor la revise entera, que estén las tablas del
    /// negocio y que se puedan contar las ventas.
    /// </summary>
    private static async Task VerificarQueEsBaseDeDatosAsync(string archivo, CancellationToken ct)
    {
        var cadena = new SqliteConnectionStringBuilder
        {
            DataSource = archivo,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();

        try
        {
            await using var conexion = new SqliteConnection(cadena);
            await conexion.OpenAsync(ct).ConfigureAwait(false);

            await using (var revision = conexion.CreateCommand())
            {
                revision.CommandText = "PRAGMA quick_check";

                var resultado = await revision.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;

                if (!string.Equals(resultado, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new NegocioException(
                        $"El motor encontró daños dentro del archivo: {resultado}");
                }
            }

            await using (var tablas = conexion.CreateCommand())
            {
                var lista = string.Join(", ", TablasEsperadas.Select(t => $"'{t}'"));

                tablas.CommandText =
                    $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ({lista})";

                var encontradas = Convert.ToInt32(
                    await tablas.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);

                if (encontradas < TablasEsperadas.Length)
                {
                    throw new NegocioException(
                        "El archivo es una base de datos, pero no es la del sistema: le faltan tablas.");
                }
            }

            await using (var ventas = conexion.CreateCommand())
            {
                ventas.CommandText = "SELECT COUNT(*) FROM Ventas";
                await ventas.ExecuteScalarAsync(ct).ConfigureAwait(false);
            }
        }
        catch (NegocioException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new NegocioException($"No se pudo abrir el archivo para comprobarlo. {ex.Message}", ex);
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

        // El comodín «*.db» no recoge los «.db.parcial»: una copia a medio escribir no
        // debe aparecer nunca en la lista desde la que el usuario elige qué restaurar.
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
