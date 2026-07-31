using Papeleria.Business.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.Business.Dtos;

/// <summary>Fila del kardex tal como se muestra en la grilla y en los reportes.</summary>
public class MovimientoKardexDto
{
    public int Id { get; init; }

    public DateTime Fecha { get; init; }

    public int ProductoId { get; init; }

    public string ProductoCodigo { get; init; } = string.Empty;

    public string ProductoNombre { get; init; } = string.Empty;

    public string UnidadAbreviatura { get; init; } = string.Empty;

    public TipoMovimientoKardex Tipo { get; init; }

    public decimal Cantidad { get; init; }

    public decimal Entrada { get; init; }

    public decimal Salida { get; init; }

    public decimal StockAnterior { get; init; }

    public decimal StockNuevo { get; init; }

    public decimal CostoUnitario { get; init; }

    public string UsuarioNombre { get; init; } = string.Empty;

    public string Motivo { get; init; } = string.Empty;

    public string? DocumentoReferencia { get; init; }

    public string TipoTexto => Tipo.Descripcion();

    public decimal ValorTotal => Dinero.Redondear(Cantidad * CostoUnitario);

    public bool EsEntrada => Entrada > 0;

    public bool EsSalida => Salida > 0;
}

/// <summary>Criterios de consulta del kardex.</summary>
public class FiltroKardex
{
    public int? ProductoId { get; set; }

    public DateTime? Desde { get; set; }

    public DateTime? Hasta { get; set; }

    public TipoMovimientoKardex? Tipo { get; set; }

    public int? UsuarioId { get; set; }

    /// <summary>Busca en nombre y código del producto, motivo y documento.</summary>
    public string? Texto { get; set; }

    public int Pagina { get; set; } = 1;

    public int TamanoPagina { get; set; } = 50;
}
