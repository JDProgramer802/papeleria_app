using Papeleria.Domain.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Domain.Entities;

/// <summary>
/// Asiento inmutable del kardex. Se escribe una única vez: la base de datos instala
/// disparadores que rechazan UPDATE y DELETE sobre esta tabla, y el contexto de datos
/// bloquea cualquier intento de modificación desde código.
/// </summary>
public class MovimientoKardex : EntidadBase
{
    public DateTime Fecha { get; set; } = DateTime.Now;

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public TipoMovimientoKardex Tipo { get; set; }

    /// <summary>Cantidad siempre positiva; la naturaleza la define <see cref="Tipo"/>.</summary>
    public decimal Cantidad { get; set; }

    public decimal Entrada { get; set; }

    public decimal Salida { get; set; }

    public decimal StockAnterior { get; set; }

    public decimal StockNuevo { get; set; }

    public decimal CostoUnitario { get; set; }

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public string Motivo { get; set; } = string.Empty;

    /// <summary>Número del documento que originó el movimiento (factura, compra, ajuste).</summary>
    public string? DocumentoReferencia { get; set; }

    /// <summary>Valor total del movimiento (cantidad × costo unitario).</summary>
    public decimal ValorTotal => Math.Round(Cantidad * CostoUnitario, 2);
}
