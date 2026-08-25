using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Papeleria.Domain.Entities;
using Papeleria.Domain.Enums;

namespace Papeleria.Data.Configurations;

public class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.ToTable("Productos");
        builder.HasKey(p => p.Id);

                builder.Property(p => p.Tipo).HasConversion<int>().HasDefaultValue(TipoProducto.Producto);
        builder.Property(p => p.UnidadesPorPresentacion).HasDefaultValue(1d);

        // Los servicios se listan aparte de la mercancía en inventario y reportes.
        builder.HasIndex(p => p.Tipo).HasDatabaseName("IX_Productos_Tipo");

builder.Property(p => p.Codigo).IsRequired().HasMaxLength(40);
        builder.Property(p => p.CodigoBarras).HasMaxLength(60);
        builder.Property(p => p.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Descripcion).HasMaxLength(600);
        builder.Property(p => p.ImagenPath).HasMaxLength(400);
        builder.Property(p => p.Ubicacion).HasMaxLength(100);
        builder.Property(p => p.Observaciones).HasMaxLength(600);
        builder.Property(p => p.Activo).HasDefaultValue(true);

        // Las propiedades calculadas del dominio no se persisten.
        builder.Ignore(p => p.UtilidadUnitaria);
        builder.Ignore(p => p.MargenPorcentaje);
        builder.Ignore(p => p.EstaAgotado);
        builder.Ignore(p => p.ControlaExistencias);
        builder.Ignore(p => p.PresentacionTexto);
        builder.Ignore(p => p.EstaBajoMinimo);

        builder.HasIndex(p => p.Codigo).IsUnique().HasDatabaseName("IX_Productos_Codigo");
        builder.HasIndex(p => p.CodigoBarras).IsUnique().HasDatabaseName("IX_Productos_CodigoBarras");
        builder.HasIndex(p => p.Nombre).HasDatabaseName("IX_Productos_Nombre");
        builder.HasIndex(p => p.CategoriaId).HasDatabaseName("IX_Productos_CategoriaId");
        builder.HasIndex(p => p.MarcaId).HasDatabaseName("IX_Productos_MarcaId");
        builder.HasIndex(p => p.Activo).HasDatabaseName("IX_Productos_Activo");

        builder.HasOne(p => p.Categoria)
            .WithMany(c => c.Productos)
            .HasForeignKey(p => p.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Marca)
            .WithMany(m => m.Productos)
            .HasForeignKey(p => p.MarcaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.UnidadMedida)
            .WithMany(u => u.Productos)
            .HasForeignKey(p => p.UnidadMedidaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
