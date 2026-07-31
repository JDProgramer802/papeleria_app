using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Papeleria.Domain.Entities;

namespace Papeleria.Data.Configurations;

public class CajaSesionConfiguration : IEntityTypeConfiguration<CajaSesion>
{
    public void Configure(EntityTypeBuilder<CajaSesion> builder)
    {
        builder.ToTable("CajaSesiones");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Estado).HasConversion<int>();
        builder.Property(s => s.ObservacionesApertura).HasMaxLength(500);
        builder.Property(s => s.ObservacionesCierre).HasMaxLength(500);

        builder.HasIndex(s => s.Estado).HasDatabaseName("IX_CajaSesiones_Estado");
        builder.HasIndex(s => s.FechaApertura).HasDatabaseName("IX_CajaSesiones_FechaApertura");

        builder.HasOne(s => s.UsuarioApertura)
            .WithMany()
            .HasForeignKey(s => s.UsuarioAperturaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.UsuarioCierre)
            .WithMany()
            .HasForeignKey(s => s.UsuarioCierreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Movimientos)
            .WithOne(m => m.CajaSesion)
            .HasForeignKey(m => m.CajaSesionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MovimientoCajaConfiguration : IEntityTypeConfiguration<MovimientoCaja>
{
    public void Configure(EntityTypeBuilder<MovimientoCaja> builder)
    {
        builder.ToTable("MovimientosCaja");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Tipo).HasConversion<int>();
        builder.Property(m => m.Concepto).IsRequired().HasMaxLength(300);
        builder.Property(m => m.AfectaEfectivo).HasDefaultValue(true);

        builder.HasIndex(m => m.CajaSesionId).HasDatabaseName("IX_MovimientosCaja_CajaSesionId");
        builder.HasIndex(m => m.Fecha).HasDatabaseName("IX_MovimientosCaja_Fecha");
        builder.HasIndex(m => m.Tipo).HasDatabaseName("IX_MovimientosCaja_Tipo");

        builder.HasOne(m => m.Usuario)
            .WithMany()
            .HasForeignKey(m => m.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Venta)
            .WithMany()
            .HasForeignKey(m => m.VentaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
