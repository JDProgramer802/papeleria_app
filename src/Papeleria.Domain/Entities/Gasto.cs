using Papeleria.Domain.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Domain.Entities;

public class Gasto : EntidadBase
{
    public DateTime Fecha { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string? Beneficiario { get; set; }
    public string? Referencia { get; set; }
    public decimal Monto { get; set; }
    public MetodoPago MetodoPago { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public int? MovimientoCajaId { get; set; }
    public MovimientoCaja? MovimientoCaja { get; set; }
    public bool Anulado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }
    public int? UsuarioAnulacionId { get; set; }
    public Usuario? UsuarioAnulacion { get; set; }
    public int? MovimientoReversionId { get; set; }
    public MovimientoCaja? MovimientoReversion { get; set; }
}
