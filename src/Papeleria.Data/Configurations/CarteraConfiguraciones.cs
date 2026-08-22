using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Papeleria.Domain.Entities;

namespace Papeleria.Data.Configurations;

public class AbonoClienteConfiguration : IEntityTypeConfiguration<AbonoCliente>
{
    public void Configure(EntityTypeBuilder<AbonoCliente> builder)
    {
        builder.ToTable("AbonosCliente");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.MetodoPago).HasConversion<int>();
        builder.Property(a => a.Observaciones).HasMaxLength(600);
        builder.Property(a => a.MotivoAnulacion).HasMaxLength(400);
        builder.Property(a => a.Anulado).HasDefaultValue(false);

        // El saldo se recalcula constantemente por cliente y por fecha.
        builder.HasIndex(a => a.ClienteId).HasDatabaseName("IX_AbonosCliente_ClienteId");
        builder.HasIndex(a => a.Fecha).HasDatabaseName("IX_AbonosCliente_Fecha");
        builder.HasIndex(a => a.CajaSesionId).HasDatabaseName("IX_AbonosCliente_CajaSesionId");

        builder.HasOne(a => a.Cliente)
            .WithMany(c => c.Abonos)
            .HasForeignKey(a => a.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Usuario)
            .WithMany()
            .HasForeignKey(a => a.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CajaSesion)
            .WithMany()
            .HasForeignKey(a => a.CajaSesionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
