using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Papeleria.Data.Storage;

namespace Papeleria.Data;

/// <summary>
/// Fábrica usada exclusivamente por las herramientas de diseño (<c>dotnet ef</c>)
/// para generar migraciones sin necesidad de arrancar la aplicación WPF.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        RutasAplicacion.AsegurarCarpetas();

        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(RutasAplicacion.CadenaConexion, sql => sql.CommandTimeout(60))
            .Options;

        return new AppDbContext(opciones);
    }
}
