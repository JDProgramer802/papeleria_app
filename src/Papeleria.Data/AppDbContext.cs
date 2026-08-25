using Microsoft.EntityFrameworkCore;
using Papeleria.Domain.Common;
using Papeleria.Domain.Entities;
using Papeleria.Domain.Exceptions;

namespace Papeleria.Data;

/// <summary>
/// Contexto de datos de la aplicación. Se resuelve mediante <c>IDbContextFactory</c>
/// para mantener instancias de vida corta y evitar bloqueos en la interfaz.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<PermisoRol> Permisos => Set<PermisoRol>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Marca> Marcas => Set<Marca>();
    public DbSet<UnidadMedida> UnidadesMedida => Set<UnidadMedida>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Compra> Compras => Set<Compra>();
    public DbSet<CompraDetalle> CompraDetalles => Set<CompraDetalle>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<VentaDetalle> VentaDetalles => Set<VentaDetalle>();
    public DbSet<MovimientoKardex> MovimientosKardex => Set<MovimientoKardex>();
    public DbSet<CajaSesion> CajaSesiones => Set<CajaSesion>();
    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>();
    public DbSet<AbonoCliente> AbonosCliente => Set<AbonoCliente>();
    public DbSet<Devolucion> Devoluciones => Set<Devolucion>();
    public DbSet<DevolucionDetalle> DevolucionDetalles => Set<DevolucionDetalle>();
    public DbSet<Configuracion> Configuraciones => Set<Configuracion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // SQLite no tiene un tipo decimal nativo: EF lo guardaría como TEXT y las
        // agregaciones (SUM, ORDER BY) dejarían de traducirse a SQL. Mapear a REAL
        // permite sumar y ordenar en el motor; los importes se redondean en la capa
        // de negocio antes de persistirse.
        foreach (var propiedad in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            propiedad.SetProviderClrType(typeof(double));
        }
    }

    public override int SaveChanges()
    {
        AplicarReglasDeGuardado();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AplicarReglasDeGuardado();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Sella las marcas de auditoría y garantiza que el kardex sea de solo escritura.
    /// La base de datos refuerza la misma regla con disparadores.
    /// </summary>
    private void AplicarReglasDeGuardado()
    {
        foreach (var entrada in ChangeTracker.Entries<MovimientoKardex>())
        {
            if (entrada.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new NegocioException(
                    "Los movimientos del kardex son inmutables: no pueden modificarse ni eliminarse.");
            }
        }

        var ahora = DateTime.Now;

        foreach (var entrada in ChangeTracker.Entries<EntidadBase>())
        {
            switch (entrada.State)
            {
                case EntityState.Added:
                    if (entrada.Entity.FechaCreacion == default)
                    {
                        entrada.Entity.FechaCreacion = ahora;
                    }
                    break;

                case EntityState.Modified:
                    entrada.Entity.FechaModificacion = ahora;
                    entrada.Property(e => e.FechaCreacion).IsModified = false;
                    break;
            }
        }
    }
}
