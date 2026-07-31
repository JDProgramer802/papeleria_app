using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Papeleria.Domain.Entities;

namespace Papeleria.Data.Configurations;

public class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("Categorias");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Descripcion).HasMaxLength(300);
        builder.Property(c => c.Activo).HasDefaultValue(true);

        builder.HasIndex(c => c.Nombre).IsUnique().HasDatabaseName("IX_Categorias_Nombre");
    }
}

public class MarcaConfiguration : IEntityTypeConfiguration<Marca>
{
    public void Configure(EntityTypeBuilder<Marca> builder)
    {
        builder.ToTable("Marcas");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Descripcion).HasMaxLength(300);
        builder.Property(m => m.Activo).HasDefaultValue(true);

        builder.HasIndex(m => m.Nombre).IsUnique().HasDatabaseName("IX_Marcas_Nombre");
    }
}

public class UnidadMedidaConfiguration : IEntityTypeConfiguration<UnidadMedida>
{
    public void Configure(EntityTypeBuilder<UnidadMedida> builder)
    {
        builder.ToTable("UnidadesMedida");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Nombre).IsRequired().HasMaxLength(60);
        builder.Property(u => u.Abreviatura).IsRequired().HasMaxLength(10);
        builder.Property(u => u.Descripcion).HasMaxLength(300);
        builder.Property(u => u.Activo).HasDefaultValue(true);

        builder.HasIndex(u => u.Nombre).IsUnique().HasDatabaseName("IX_UnidadesMedida_Nombre");
    }
}
