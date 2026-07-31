using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Papeleria.Domain.Entities;

namespace Papeleria.Data.Configurations;

public class MovimientoKardexConfiguration : IEntityTypeConfiguration<MovimientoKardex>
{
    public void Configure(EntityTypeBuilder<MovimientoKardex> builder)
    {
        builder.ToTable("MovimientosKardex");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Tipo).HasConversion<int>();
        builder.Property(m => m.Motivo).IsRequired().HasMaxLength(300);
        builder.Property(m => m.DocumentoReferencia).HasMaxLength(50);

        builder.Ignore(m => m.ValorTotal);

        builder.HasIndex(m => m.Fecha).HasDatabaseName("IX_MovimientosKardex_Fecha");
        builder.HasIndex(m => m.ProductoId).HasDatabaseName("IX_MovimientosKardex_ProductoId");
        builder.HasIndex(m => m.Tipo).HasDatabaseName("IX_MovimientosKardex_Tipo");
        builder.HasIndex(m => new { m.ProductoId, m.Fecha }).HasDatabaseName("IX_MovimientosKardex_Producto_Fecha");

        builder.HasOne(m => m.Producto)
            .WithMany(p => p.MovimientosKardex)
            .HasForeignKey(m => m.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Usuario)
            .WithMany(u => u.MovimientosKardex)
            .HasForeignKey(m => m.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
