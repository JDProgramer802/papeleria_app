using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Papeleria.Domain.Entities;

namespace Papeleria.Data.Configurations;

public class VentaConfiguration : IEntityTypeConfiguration<Venta>
{
    public void Configure(EntityTypeBuilder<Venta> builder)
    {
        builder.ToTable("Ventas");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.NumeroFactura).IsRequired().HasMaxLength(30);
        builder.Property(v => v.MetodoPago).HasConversion<int>();
        builder.Property(v => v.Estado).HasConversion<int>();
        builder.Property(v => v.MotivoAnulacion).HasMaxLength(400);
        builder.Property(v => v.Observaciones).HasMaxLength(600);

        builder.Ignore(v => v.Utilidad);

        builder.HasIndex(v => v.NumeroFactura).IsUnique().HasDatabaseName("IX_Ventas_NumeroFactura");
        builder.HasIndex(v => v.Fecha).HasDatabaseName("IX_Ventas_Fecha");
        builder.HasIndex(v => v.ClienteId).HasDatabaseName("IX_Ventas_ClienteId");
        builder.HasIndex(v => v.Estado).HasDatabaseName("IX_Ventas_Estado");
        builder.HasIndex(v => v.CajaSesionId).HasDatabaseName("IX_Ventas_CajaSesionId");

        builder.HasOne(v => v.Cliente)
            .WithMany(c => c.Ventas)
            .HasForeignKey(v => v.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Usuario)
            .WithMany(u => u.Ventas)
            .HasForeignKey(v => v.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.CajaSesion)
            .WithMany(s => s.Ventas)
            .HasForeignKey(v => v.CajaSesionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(v => v.Detalles)
            .WithOne(d => d.Venta)
            .HasForeignKey(d => d.VentaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VentaDetalleConfiguration : IEntityTypeConfiguration<VentaDetalle>
{
    public void Configure(EntityTypeBuilder<VentaDetalle> builder)
    {
        builder.ToTable("VentaDetalles");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DescripcionProducto).IsRequired().HasMaxLength(200);

        builder.HasIndex(d => d.VentaId).HasDatabaseName("IX_VentaDetalles_VentaId");
        builder.HasIndex(d => d.ProductoId).HasDatabaseName("IX_VentaDetalles_ProductoId");

        builder.HasOne(d => d.Producto)
            .WithMany(p => p.VentaDetalles)
            .HasForeignKey(d => d.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
