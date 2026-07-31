using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Papeleria.Domain.Entities;

namespace Papeleria.Data.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.NombreUsuario).IsRequired().HasMaxLength(50);
        builder.Property(u => u.NombreCompleto).IsRequired().HasMaxLength(150);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Correo).HasMaxLength(150);
        builder.Property(u => u.Telefono).HasMaxLength(30);
        builder.Property(u => u.Rol).HasConversion<int>();
        builder.Property(u => u.Activo).HasDefaultValue(true);

        builder.HasIndex(u => u.NombreUsuario).IsUnique().HasDatabaseName("IX_Usuarios_NombreUsuario");
        builder.HasIndex(u => u.Rol).HasDatabaseName("IX_Usuarios_Rol");
    }
}

public class PermisoRolConfiguration : IEntityTypeConfiguration<PermisoRol>
{
    public void Configure(EntityTypeBuilder<PermisoRol> builder)
    {
        builder.ToTable("Permisos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Rol).HasConversion<int>();
        builder.Property(p => p.Modulo).IsRequired().HasMaxLength(50);

        builder.HasIndex(p => new { p.Rol, p.Modulo })
            .IsUnique()
            .HasDatabaseName("IX_Permisos_Rol_Modulo");
    }
}

public class ConfiguracionConfiguration : IEntityTypeConfiguration<Configuracion>
{
    public void Configure(EntityTypeBuilder<Configuracion> builder)
    {
        builder.ToTable("Configuraciones");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Clave).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Valor).HasMaxLength(1000);
        builder.Property(c => c.Descripcion).HasMaxLength(300);

        builder.HasIndex(c => c.Clave).IsUnique().HasDatabaseName("IX_Configuraciones_Clave");
    }
}
