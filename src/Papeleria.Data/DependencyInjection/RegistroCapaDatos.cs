using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Papeleria.Data.Repositories;
using Papeleria.Data.Seed;
using Papeleria.Data.Storage;

namespace Papeleria.Data.DependencyInjection;

/// <summary>Registro en el contenedor de dependencias de todo lo que expone la capa de datos.</summary>
public static class RegistroCapaDatos
{
    public static IServiceCollection AgregarCapaDatos(this IServiceCollection servicios)
    {
        RutasAplicacion.AsegurarCarpetas();

        // Fábrica agrupada: los contextos son de vida corta y se reutiliza el pool,
        // evitando el coste de construir el modelo en cada operación.
        servicios.AddPooledDbContextFactory<AppDbContext>(opciones =>
        {
            opciones.UseSqlite(RutasAplicacion.CadenaConexion, sql =>
            {
                sql.CommandTimeout(60);
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name);
            });

            opciones.EnableSensitiveDataLogging(false);
            opciones.EnableDetailedErrors(false);
        });

        servicios.AddSingleton<IUnidadDeTrabajoFactory, UnidadDeTrabajoFactory>();
        servicios.AddSingleton<SembradorDatos>();
        servicios.AddSingleton<IInicializadorBaseDatos, InicializadorBaseDatos>();

        return servicios;
    }
}
