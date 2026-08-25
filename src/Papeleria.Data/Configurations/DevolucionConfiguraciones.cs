using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Papeleria.Domain.Entities;

namespace Papeleria.Data.Configurations;

public class DevolucionConfiguration : IEntityTypeConfiguration<Devolucion>
{
    public void Configure(EntityTypeBuilder<Devolucion> builder)
    {
        builder.ToTable("Devoluciones");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Numero).IsRequired().HasMaxLength(30);
        builder.Property(d => d.Motivo).IsRequired().HasMaxLength(400);

        builder.HasIndex(d => d.Numero).IsUnique().HasDatabaseName("IX_Devoluciones_Numero");
        builder.HasIndex(d => d.VentaId).HasDatabaseName("IX_Devoluciones_VentaId");
        builder.HasIndex(d => d.Fecha).HasDatabaseName("IX_Devoluciones_Fecha");
        builder.HasIndex(d => d.CajaSesionId).HasDatabaseName("IX_Devoluciones_CajaSesionId");

        builder.HasOne(d => d.Venta)
            .WithMany()
            .HasForeignKey(d => d.VentaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Usuario)
            .WithMany()
            .HasForeignKey(d => d.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.CajaSesion)
            .WithMany()
            .HasForeignKey(d => d.CajaSesionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(d => d.Detalles)
            .WithOne(x => x.Devolucion)
            .HasForeignKey(x => x.DevolucionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DevolucionDetalleConfiguration : IEntityTypeConfiguration<DevolucionDetalle>
{
    public void Configure(EntityTypeBuilder<DevolucionDetalle> builder)
    {
        builder.ToTable("DevolucionDetalles");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DescripcionProducto).IsRequired().HasMaxLength(200);

        builder.HasIndex(d => d.DevolucionId).HasDatabaseName("IX_DevolucionDetalles_DevolucionId");
        builder.HasIndex(d => d.ProductoId).HasDatabaseName("IX_DevolucionDetalles_ProductoId");

        builder.HasOne(d => d.Producto)
            .WithMany()
            .HasForeignKey(d => d.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
