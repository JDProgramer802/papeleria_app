using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Papeleria.Domain.Entities;

namespace Papeleria.Data.Configurations;

public class ProveedorConfiguration : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> builder)
    {
        builder.ToTable("Proveedores");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nombre).IsRequired().HasMaxLength(180);
        builder.Property(p => p.Nit).HasMaxLength(40);
        builder.Property(p => p.Contacto).HasMaxLength(150);
        builder.Property(p => p.Telefono).HasMaxLength(40);
        builder.Property(p => p.Correo).HasMaxLength(150);
        builder.Property(p => p.Direccion).HasMaxLength(250);
        builder.Property(p => p.Ciudad).HasMaxLength(100);
        builder.Property(p => p.Observaciones).HasMaxLength(600);
        builder.Property(p => p.Activo).HasDefaultValue(true);

        // SQLite trata cada NULL como distinto, por lo que el índice único
        // admite varios proveedores sin NIT registrado.
        builder.HasIndex(p => p.Nit).IsUnique().HasDatabaseName("IX_Proveedores_Nit");
        builder.HasIndex(p => p.Nombre).HasDatabaseName("IX_Proveedores_Nombre");
    }
}

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Clientes");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nombre).IsRequired().HasMaxLength(180);
        builder.Property(c => c.TipoDocumento).HasConversion<int>();
        builder.Property(c => c.NumeroDocumento).HasMaxLength(40);
        builder.Property(c => c.Telefono).HasMaxLength(40);
        builder.Property(c => c.Correo).HasMaxLength(150);
        builder.Property(c => c.Direccion).HasMaxLength(250);
        builder.Property(c => c.Ciudad).HasMaxLength(100);
        builder.Property(c => c.Observaciones).HasMaxLength(600);
        builder.Property(c => c.Activo).HasDefaultValue(true);
        builder.Property(c => c.LimiteCredito).HasDefaultValue(0d);

        builder.HasIndex(c => c.NumeroDocumento).IsUnique().HasDatabaseName("IX_Clientes_NumeroDocumento");
        builder.HasIndex(c => c.Nombre).HasDatabaseName("IX_Clientes_Nombre");
    }
}
