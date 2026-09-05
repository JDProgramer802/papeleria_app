using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Papeleria.Domain.Entities;

namespace Papeleria.Data.Configurations;

public class CotizacionConfiguration : IEntityTypeConfiguration<Cotizacion>
{
    public void Configure(EntityTypeBuilder<Cotizacion> builder)
    {
        builder.ToTable("Cotizaciones");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Numero).IsRequired().HasMaxLength(30);
        builder.Property(c => c.Estado).HasConversion<int>();
        builder.Property(c => c.Observaciones).HasMaxLength(800);

        builder.HasIndex(c => c.Numero).IsUnique().HasDatabaseName("IX_Cotizaciones_Numero");
        builder.HasIndex(c => c.Fecha).HasDatabaseName("IX_Cotizaciones_Fecha");
        builder.HasIndex(c => c.ClienteId).HasDatabaseName("IX_Cotizaciones_ClienteId");
        builder.HasIndex(c => c.Estado).HasDatabaseName("IX_Cotizaciones_Estado");

        builder.HasOne(c => c.Cliente)
            .WithMany()
            .HasForeignKey(c => c.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Usuario)
            .WithMany()
            .HasForeignKey(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // Si algún día se anulara la venta, la cotización se queda sin apuntar a nada
        // pero no desaparece: el histórico de lo que se cotizó no se toca.
        builder.HasOne(c => c.Venta)
            .WithMany()
            .HasForeignKey(c => c.VentaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Detalles)
            .WithOne(d => d.Cotizacion)
            .HasForeignKey(d => d.CotizacionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CotizacionDetalleConfiguration : IEntityTypeConfiguration<CotizacionDetalle>
{
    public void Configure(EntityTypeBuilder<CotizacionDetalle> builder)
    {
        builder.ToTable("CotizacionDetalles");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DescripcionProducto).IsRequired().HasMaxLength(200);

        builder.HasIndex(d => d.CotizacionId).HasDatabaseName("IX_CotizacionDetalles_CotizacionId");
        builder.HasIndex(d => d.ProductoId).HasDatabaseName("IX_CotizacionDetalles_ProductoId");

        builder.HasOne(d => d.Producto)
            .WithMany()
            .HasForeignKey(d => d.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
