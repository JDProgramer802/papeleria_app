using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Papeleria.Data.Seed;
using Papeleria.Data.Storage;

namespace Papeleria.Data;

/// <summary>Reporta el avance del arranque para que la pantalla de carga lo muestre.</summary>
public record AvanceInicializacion(string Mensaje, int Porcentaje);

/// <summary>
/// Prepara la base de datos en cada arranque: crea las carpetas, aplica las migraciones
/// pendientes y siembra los datos maestros. Es seguro ejecutarlo siempre.
/// </summary>
public interface IInicializadorBaseDatos
{
    Task InicializarAsync(IProgress<AvanceInicializacion>? progreso = null, CancellationToken ct = default);
}

/// <inheritdoc cref="IInicializadorBaseDatos" />
public class InicializadorBaseDatos : IInicializadorBaseDatos
{
    private readonly IDbContextFactory<AppDbContext> _fabricaContexto;
    private readonly SembradorDatos _sembrador;
    private readonly ILogger<InicializadorBaseDatos> _log;

    public InicializadorBaseDatos(
        IDbContextFactory<AppDbContext> fabricaContexto,
        SembradorDatos sembrador,
        ILogger<InicializadorBaseDatos> log)
    {
        _fabricaContexto = fabricaContexto;
        _sembrador = sembrador;
        _log = log;
    }

    public async Task InicializarAsync(IProgress<AvanceInicializacion>? progreso = null, CancellationToken ct = default)
    {
        progreso?.Report(new AvanceInicializacion("Preparando carpetas de trabajo…", 10));
        RutasAplicacion.AsegurarCarpetas();

        await using var contexto = await _fabricaContexto.CreateDbContextAsync(ct).ConfigureAwait(false);

        progreso?.Report(new AvanceInicializacion("Verificando la base de datos…", 25));

        var pendientes = (await contexto.Database.GetPendingMigrationsAsync(ct).ConfigureAwait(false)).ToList();

        if (pendientes.Count > 0)
        {
            _log.LogInformation("Aplicando {Cantidad} migración(es): {Migraciones}",
                pendientes.Count, string.Join(", ", pendientes));

            progreso?.Report(new AvanceInicializacion(
                pendientes.Count == 1
                    ? "Aplicando 1 actualización de la base de datos…"
                    : $"Aplicando {pendientes.Count} actualizaciones de la base de datos…", 45));

            await contexto.Database.MigrateAsync(ct).ConfigureAwait(false);
        }

        progreso?.Report(new AvanceInicializacion("Aplicando optimizaciones…", 65));
        await AplicarAjustesSqliteAsync(contexto, ct).ConfigureAwait(false);

        progreso?.Report(new AvanceInicializacion("Cargando datos maestros…", 80));
        await _sembrador.SembrarAsync(contexto, ct).ConfigureAwait(false);

        progreso?.Report(new AvanceInicializacion("Base de datos lista.", 100));
        _log.LogInformation("Base de datos inicializada en {Ruta}", RutasAplicacion.ArchivoBaseDatos);
    }

    /// <summary>
    /// Ajustes de rendimiento y consistencia de SQLite. WAL mejora la concurrencia entre
    /// lectura y escritura, y las claves foráneas quedan activas en cada conexión.
    /// </summary>
    private static async Task AplicarAjustesSqliteAsync(AppDbContext contexto, CancellationToken ct)
    {
        await contexto.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct).ConfigureAwait(false);
        await contexto.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;", ct).ConfigureAwait(false);
        await contexto.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", ct).ConfigureAwait(false);
        await contexto.Database.ExecuteSqlRawAsync("PRAGMA temp_store=MEMORY;", ct).ConfigureAwait(false);
    }
}
