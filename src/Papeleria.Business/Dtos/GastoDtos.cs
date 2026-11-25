using Papeleria.Domain.Enums;

namespace Papeleria.Business.Dtos;

public record SolicitudGasto(DateTime Fecha, string Concepto, string Categoria, decimal Monto,
    MetodoPago MetodoPago, string? Beneficiario = null, string? Referencia = null);

public record FiltroGastos(DateTime Desde, DateTime Hasta, string? Texto = null,
    string? Categoria = null, bool IncluirAnulados = false);

public record GastoDto(int Id, DateTime Fecha, string Concepto, string Categoria, decimal Monto,
    MetodoPago MetodoPago, string? Beneficiario, string? Referencia, bool Anulado,
    string Usuario, string? MotivoAnulacion)
{
    public string Estado => Anulado ? "Anulado" : "Registrado";
    public string Pago => Common.Enumeraciones.Descripcion(MetodoPago);
}
