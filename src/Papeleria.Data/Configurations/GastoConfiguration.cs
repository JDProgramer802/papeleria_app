using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Papeleria.Domain.Entities;

namespace Papeleria.Data.Configurations;

public class GastoConfiguration : IEntityTypeConfiguration<Gasto>
{
    public void Configure(EntityTypeBuilder<Gasto> b)
    {
        b.ToTable("Gastos");
        b.HasKey(g => g.Id);
        b.Property(g => g.Concepto).HasMaxLength(250).IsRequired();
        b.Property(g => g.Categoria).HasMaxLength(80).IsRequired();
        b.Property(g => g.Beneficiario).HasMaxLength(150);
        b.Property(g => g.Referencia).HasMaxLength(100);
        b.Property(g => g.MotivoAnulacion).HasMaxLength(500);
        b.HasIndex(g => g.Fecha);
        b.HasOne(g => g.Usuario).WithMany().HasForeignKey(g => g.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(g => g.UsuarioAnulacion).WithMany().HasForeignKey(g => g.UsuarioAnulacionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(g => g.MovimientoCaja).WithMany().HasForeignKey(g => g.MovimientoCajaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(g => g.MovimientoReversion).WithMany().HasForeignKey(g => g.MovimientoReversionId).OnDelete(DeleteBehavior.Restrict);
    }
}
