using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Papeleria.Domain.Entities;

namespace Papeleria.Data.Configurations;

public class CompraConfiguration : IEntityTypeConfiguration<Compra>
{
    public void Configure(EntityTypeBuilder<Compra> builder)
    {
        builder.ToTable("Compras");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Numero).IsRequired().HasMaxLength(30);
        builder.Property(c => c.NumeroFacturaProveedor).HasMaxLength(50);
        builder.Property(c => c.Observaciones).HasMaxLength(600);
        builder.Property(c => c.Estado).HasConversion<int>();

        builder.HasIndex(c => c.Numero).IsUnique().HasDatabaseName("IX_Compras_Numero");
        builder.HasIndex(c => c.Fecha).HasDatabaseName("IX_Compras_Fecha");
        builder.HasIndex(c => c.ProveedorId).HasDatabaseName("IX_Compras_ProveedorId");

        builder.HasOne(c => c.Proveedor)
            .WithMany(p => p.Compras)
            .HasForeignKey(c => c.ProveedorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Usuario)
            .WithMany(u => u.Compras)
            .HasForeignKey(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Detalles)
            .WithOne(d => d.Compra)
            .HasForeignKey(d => d.CompraId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CompraDetalleConfiguration : IEntityTypeConfiguration<CompraDetalle>
{
    public void Configure(EntityTypeBuilder<CompraDetalle> builder)
    {
        builder.ToTable("CompraDetalles");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DescripcionProducto).IsRequired().HasMaxLength(200);

        builder.HasIndex(d => d.CompraId).HasDatabaseName("IX_CompraDetalles_CompraId");
        builder.HasIndex(d => d.ProductoId).HasDatabaseName("IX_CompraDetalles_ProductoId");

        builder.HasOne(d => d.Producto)
            .WithMany(p => p.CompraDetalles)
            .HasForeignKey(d => d.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
