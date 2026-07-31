using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Papeleria.Data.Storage;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Exceptions;

namespace Papeleria.Business.Services;

/// <inheritdoc cref="IServicioActualizaciones" />
public class ServicioActualizaciones : IServicioActualizaciones
{
    /// <summary>Sufijo del ejecutable anterior mientras espera a ser borrado.</summary>
    private const string SufijoAnterior = ".anterior";

    private readonly IHttpClientFactory _fabricaHttp;
    private readonly IServicioConfiguracion _configuracion;
    private readonly ILogger<ServicioActualizaciones> _log;

    public ServicioActualizaciones(
        IHttpClientFactory fabricaHttp,
        IServicioConfiguracion configuracion,
        ILogger<ServicioActualizaciones> log)
    {
        _fabricaHttp = fabricaHttp;
        _configuracion = configuracion;
        _log = log;

        VersionActual = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);
    }

    public Version VersionActual { get; }

    private static string? RutaEjecutable => Environment.ProcessPath;

    public ImpedimentoActualizacion ComprobarViabilidad()
    {
        var ejecutable = RutaEjecutable;

        if (string.IsNullOrWhiteSpace(ejecutable) || !File.Exists(ejecutable))
        {
            return ImpedimentoActualizacion.NoEsEjecutablePublicado;
        }

        var carpeta = Path.GetDirectoryName(ejecutable);

        if (string.IsNullOrWhiteSpace(carpeta))
        {
            return ImpedimentoActualizacion.NoEsEjecutablePublicado;
        }

        // En la compilación de desarrollo el .exe es solo un lanzador y la aplicación
        // vive en el .dll de al lado; reemplazar el .exe la dejaría inservible. En la
        // publicación de archivo único ese .dll va incrustado y no existe en disco.
        // El nombre se deriva del ejecutable en marcha para que la detección siga
        // siendo correcta aunque se renombre el archivo.
        if (File.Exists(Path.ChangeExtension(ejecutable, ".dll")))
        {
            return ImpedimentoActualizacion.NoEsEjecutablePublicado;
        }

        // Si el programa está en Archivos de programa hará falta elevación; se detecta
        // intentando escribir realmente, que es lo único fiable en Windows.
        try
        {
            var prueba = Path.Combine(carpeta, $".escritura_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(prueba, string.Empty);
            File.Delete(prueba);
        }
        catch (Exception)
        {
            return ImpedimentoActualizacion.CarpetaSoloLectura;
        }

        return ImpedimentoActualizacion.Ninguno;
    }

    public async Task<ActualizacionDisponible?> ComprobarAsync(
        bool forzar = false, CancellationToken ct = default)
    {
        var repositorio = _configuracion.ObtenerTexto(ClavesConfiguracion.ActualizacionesRepositorio);

        if (string.IsNullOrWhiteSpace(repositorio))
        {
            return null;
        }

        // Comprobación silenciosa: como mucho una vez al día para no gastar la cuota
        // de la API de GitHub ni molestar en cada arranque.
        if (!forzar)
        {
            if (!_configuracion.ObtenerBooleano(ClavesConfiguracion.ActualizacionesAutomaticas, true))
            {
                return null;
            }

            var ultima = _configuracion.ObtenerFecha(ClavesConfiguracion.ActualizacionesUltimaComprobacion);

            if (ultima is { } fecha && (DateTime.Now - fecha).TotalHours < 24)
            {
                return null;
            }
        }

        try
        {
            var publicacion = await ConsultarUltimaPublicacionAsync(repositorio, ct).ConfigureAwait(false);

            await _configuracion.GuardarAsync(
                ClavesConfiguracion.ActualizacionesUltimaComprobacion,
                DateTime.Now.ToString("O", CultureInfo.InvariantCulture), ct).ConfigureAwait(false);

            if (publicacion is null || publicacion.Version <= VersionActual)
            {
                return null;
            }

            // El usuario pudo pedir que no se le vuelva a avisar de esta versión.
            if (!forzar)
            {
                var omitida = _configuracion.ObtenerTexto(ClavesConfiguracion.ActualizacionesVersionOmitida);

                if (Version.TryParse(omitida, out var versionOmitida) && publicacion.Version <= versionOmitida)
                {
                    return null;
                }
            }

            _log.LogInformation("Actualización disponible: {Version} (actual {Actual})",
                publicacion.Version, VersionActual);

            return publicacion;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Sin conexión o GitHub no responde: la aplicación debe seguir funcionando.
            _log.LogInformation("No se pudo comprobar actualizaciones: {Motivo}", ex.Message);

            if (forzar)
            {
                throw new NegocioException(
                    "No se pudo conectar con el servidor de actualizaciones. " +
                    "Compruebe su conexión a Internet e inténtelo de nuevo.", ex);
            }

            return null;
        }
    }

    private async Task<ActualizacionDisponible?> ConsultarUltimaPublicacionAsync(
        string repositorio, CancellationToken ct)
    {
        var cliente = _fabricaHttp.CreateClient(nameof(ServicioActualizaciones));

        var respuesta = await cliente
            .GetAsync($"https://api.github.com/repos/{repositorio.Trim('/')}/releases/latest", ct)
            .ConfigureAwait(false);

        if (!respuesta.IsSuccessStatusCode)
        {
            _log.LogInformation("GitHub respondió {Codigo} al consultar {Repositorio}",
                respuesta.StatusCode, repositorio);
            return null;
        }

        await using var contenido = await respuesta.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var documento = await JsonDocument.ParseAsync(contenido, cancellationToken: ct).ConfigureAwait(false);

        var raiz = documento.RootElement;

        if (raiz.TryGetProperty("draft", out var borrador) && borrador.GetBoolean())
        {
            return null;
        }

        var etiqueta = raiz.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null;

        if (!TryLeerVersion(etiqueta, out var version))
        {
            _log.LogWarning("La etiqueta «{Etiqueta}» no tiene un formato de versión reconocible", etiqueta);
            return null;
        }

        // Se busca el ejecutable entre los archivos adjuntos de la publicación.
        if (!raiz.TryGetProperty("assets", out var adjuntos) || adjuntos.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var adjunto in adjuntos.EnumerateArray())
        {
            var nombre = adjunto.TryGetProperty("name", out var n) ? n.GetString() : null;

            if (nombre is null || !nombre.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var url = adjunto.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;

            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            return new ActualizacionDisponible
            {
                Version = version,
                Nombre = raiz.TryGetProperty("name", out var titulo) && !string.IsNullOrWhiteSpace(titulo.GetString())
                    ? titulo.GetString()!
                    : $"Versión {version.ToString(3)}",
                Notas = raiz.TryGetProperty("body", out var cuerpo) ? cuerpo.GetString() ?? string.Empty : string.Empty,
                UrlDescarga = url,
                TamanoBytes = adjunto.TryGetProperty("size", out var tamano) ? tamano.GetInt64() : 0,
                Sha256 = LeerHuella(adjunto),
                Publicada = raiz.TryGetProperty("published_at", out var fecha) &&
                            fecha.TryGetDateTime(out var publicada)
                    ? publicada.ToLocalTime()
                    : null
            };
        }

        _log.LogWarning("La publicación {Version} no incluye ningún ejecutable adjunto", version);
        return null;
    }

    /// <summary>GitHub expone la huella como «sha256:abc…» en el campo <c>digest</c>.</summary>
    private static string? LeerHuella(JsonElement adjunto)
    {
        if (!adjunto.TryGetProperty("digest", out var digest))
        {
            return null;
        }

        var valor = digest.GetString();

        return valor is not null && valor.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? valor["sha256:".Length..]
            : null;
    }

    /// <summary>Acepta etiquetas del tipo «v1.2.3», «1.2.3» o «version-1.2.3».</summary>
    internal static bool TryLeerVersion(string? etiqueta, out Version version)
    {
        version = new Version(0, 0);

        if (string.IsNullOrWhiteSpace(etiqueta))
        {
            return false;
        }

        var limpia = new string(etiqueta.Where(c => char.IsDigit(c) || c == '.').ToArray()).Trim('.');

        return limpia.Contains('.') && Version.TryParse(limpia, out version!);
    }

    public async Task<string> DescargarAsync(
        ActualizacionDisponible actualizacion, IProgress<double>? progreso = null, CancellationToken ct = default)
    {
        var destino = Path.Combine(
            RutasAplicacion.CarpetaTemporal,
            $"Papeleria-{actualizacion.Version.ToString(3)}.exe");

        Directory.CreateDirectory(RutasAplicacion.CarpetaTemporal);

        try
        {
            var cliente = _fabricaHttp.CreateClient(nameof(ServicioActualizaciones));

            using var respuesta = await cliente
                .GetAsync(actualizacion.UrlDescarga, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            respuesta.EnsureSuccessStatusCode();

            var total = respuesta.Content.Headers.ContentLength ?? actualizacion.TamanoBytes;

            await using (var origen = await respuesta.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var archivo = File.Create(destino))
            {
                var bufer = new byte[81920];
                long copiados = 0;
                int leidos;

                while ((leidos = await origen.ReadAsync(bufer, ct).ConfigureAwait(false)) > 0)
                {
                    await archivo.WriteAsync(bufer.AsMemory(0, leidos), ct).ConfigureAwait(false);

                    copiados += leidos;

                    if (total > 0)
                    {
                        progreso?.Report(Math.Min(copiados * 100d / total, 100d));
                    }
                }
            }

            await VerificarDescargaAsync(destino, actualizacion, ct).ConfigureAwait(false);

            _log.LogInformation("Actualización {Version} descargada en {Ruta}", actualizacion.Version, destino);

            return destino;
        }
        catch (Exception ex)
        {
            // Una descarga a medias no debe quedarse ocupando disco ni poder aplicarse.
            EliminarSiExiste(destino);

            if (ex is NegocioException)
            {
                throw;
            }

            if (ex is OperationCanceledException)
            {
                throw;
            }

            throw new NegocioException(
                $"No se pudo descargar la actualización. {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Comprueba que lo descargado es exactamente lo publicado. Sin esta verificación
    /// una descarga truncada dejaría el programa inservible al sustituirlo.
    /// </summary>
    private async Task VerificarDescargaAsync(
        string archivo, ActualizacionDisponible actualizacion, CancellationToken ct)
    {
        var informacion = new FileInfo(archivo);

        if (actualizacion.TamanoBytes > 0 && informacion.Length != actualizacion.TamanoBytes)
        {
            throw new NegocioException(
                "La descarga quedó incompleta y se descartó. Inténtelo de nuevo.");
        }

        if (string.IsNullOrWhiteSpace(actualizacion.Sha256))
        {
            _log.LogInformation("La publicación no trae huella SHA-256; se validó solo el tamaño");
            return;
        }

        await using var flujo = File.OpenRead(archivo);
        var huella = await SHA256.HashDataAsync(flujo, ct).ConfigureAwait(false);
        var calculada = Convert.ToHexString(huella);

        if (!calculada.Equals(actualizacion.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new NegocioException(
                "El archivo descargado no coincide con el publicado y se descartó por seguridad.");
        }
    }

    public async Task AplicarAsync(string archivoDescargado, CancellationToken ct = default)
    {
        var impedimento = ComprobarViabilidad();

        if (impedimento != ImpedimentoActualizacion.Ninguno)
        {
            throw new NegocioException(impedimento == ImpedimentoActualizacion.CarpetaSoloLectura
                ? "No hay permisos para escribir en la carpeta del programa. " +
                  "Ejecute la aplicación como administrador o muévala a otra carpeta."
                : "Las actualizaciones automáticas solo funcionan sobre el ejecutable publicado.");
        }

        if (!File.Exists(archivoDescargado))
        {
            throw new NegocioException("El archivo de la actualización ya no está disponible.");
        }

        var actual = RutaEjecutable!;
        var anterior = actual + SufijoAnterior;

        try
        {
            EliminarSiExiste(anterior);

            // Windows permite renombrar un ejecutable en uso, pero no sobrescribirlo:
            // se aparta el actual y se deja el nuevo en su lugar. El que queda apartado
            // se borra en el siguiente arranque.
            File.Move(actual, anterior);

            try
            {
                File.Move(archivoDescargado, actual);
            }
            catch
            {
                // Si el nuevo no se pudo colocar, se restaura el original: es preferible
                // quedarse en la versión antigua que dejar al negocio sin programa.
                File.Move(anterior, actual);
                throw;
            }

            _log.LogWarning("Ejecutable sustituido por la actualización descargada");

            await Task.CompletedTask.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not NegocioException)
        {
            throw new NegocioException($"No se pudo aplicar la actualización. {ex.Message}", ex);
        }
    }

    public Task OmitirVersionAsync(Version version, CancellationToken ct = default) =>
        _configuracion.GuardarAsync(
            ClavesConfiguracion.ActualizacionesVersionOmitida, version.ToString(), ct);

    public void LimpiarRestosDeActualizacion()
    {
        var ejecutable = RutaEjecutable;

        if (string.IsNullOrWhiteSpace(ejecutable))
        {
            return;
        }

        EliminarSiExiste(ejecutable + SufijoAnterior);

        // Descargas que quedaron a medias de sesiones anteriores.
        try
        {
            if (!Directory.Exists(RutasAplicacion.CarpetaTemporal))
            {
                return;
            }

            foreach (var archivo in Directory.EnumerateFiles(RutasAplicacion.CarpetaTemporal, "Papeleria-*.exe"))
            {
                EliminarSiExiste(archivo);
            }
        }
        catch (Exception ex)
        {
            _log.LogInformation("No se pudieron limpiar los restos de actualización: {Motivo}", ex.Message);
        }
    }

    private static void EliminarSiExiste(string ruta)
    {
        try
        {
            if (File.Exists(ruta))
            {
                File.Delete(ruta);
            }
        }
        catch (IOException)
        {
            // Sigue bloqueado; se reintentará en el próximo arranque.
        }
        catch (UnauthorizedAccessException)
        {
            // Sin permisos para borrarlo; no es crítico.
        }
    }
}
