using Papeleria.Business.Common;

namespace Papeleria.Business.Dtos;

/// <summary>Semáforo de existencias mostrado en las grillas de producto e inventario.</summary>
public enum EstadoStock
{
    Agotado = 0,
    Bajo = 1,
    Normal = 2,
    Exceso = 3
}

/// <summary>Proyección de producto para listados y grillas. Evita traer entidades completas.</summary>
public class ProductoListadoDto
{
    public int Id { get; init; }

    public string Codigo { get; init; } = string.Empty;

    public string? CodigoBarras { get; init; }

    public string Nombre { get; init; } = string.Empty;

    public string? Descripcion { get; init; }

    public int CategoriaId { get; init; }

    public string CategoriaNombre { get; init; } = string.Empty;

    public int? MarcaId { get; init; }

    public string MarcaNombre { get; init; } = string.Empty;

    public int UnidadMedidaId { get; init; }

    public string UnidadAbreviatura { get; init; } = string.Empty;

    public decimal Costo { get; init; }

    public decimal PrecioVenta { get; init; }

    public decimal PorcentajeIva { get; init; }

    public decimal StockActual { get; init; }

    public decimal StockMinimo { get; init; }

    public decimal StockMaximo { get; init; }

    public string? ImagenPath { get; init; }

    public string? Ubicacion { get; init; }

    public bool Activo { get; init; }

    /// <summary>Valor del inventario de este producto a precio de costo.</summary>
    public decimal ValorInventario => Dinero.Redondear(StockActual * Costo);

    public decimal UtilidadUnitaria => Dinero.Redondear(PrecioVenta - Costo);

    public decimal MargenPorcentaje =>
        PrecioVenta <= 0 ? 0 : Math.Round((PrecioVenta - Costo) / PrecioVenta * 100m, 1);

    public decimal PrecioConIva => Dinero.Redondear(PrecioVenta * (1 + PorcentajeIva / 100m));

    public EstadoStock Estado
    {
        get
        {
            if (StockActual <= 0) return EstadoStock.Agotado;
            if (StockActual <= StockMinimo) return EstadoStock.Bajo;
            if (StockMaximo > 0 && StockActual > StockMaximo) return EstadoStock.Exceso;
            return EstadoStock.Normal;
        }
    }

    public string EstadoTexto => Estado switch
    {
        EstadoStock.Agotado => "Agotado",
        EstadoStock.Bajo => "Bajo mínimo",
        EstadoStock.Exceso => "Sobre máximo",
        _ => "Disponible"
    };
}

/// <summary>Datos mínimos que el punto de venta necesita para agregar una línea al carrito.</summary>
public class ProductoPosDto
{
    public int Id { get; init; }

    public string Codigo { get; init; } = string.Empty;

    public string? CodigoBarras { get; init; }

    public string Nombre { get; init; } = string.Empty;

    public string UnidadAbreviatura { get; init; } = string.Empty;

    public string CategoriaNombre { get; init; } = string.Empty;

    public decimal PrecioVenta { get; init; }

    public decimal Costo { get; init; }

    public decimal PorcentajeIva { get; init; }

    public decimal StockActual { get; init; }

    public string? ImagenPath { get; init; }

    public bool HayExistencias => StockActual > 0;

    public decimal PrecioConIva => Dinero.Redondear(PrecioVenta * (1 + PorcentajeIva / 100m));
}

/// <summary>Criterios de búsqueda y paginación del módulo de productos.</summary>
public class FiltroProductos
{
    /// <summary>Texto libre: busca en código, código de barras, nombre y descripción.</summary>
    public string? Texto { get; set; }

    public int? CategoriaId { get; set; }

    public int? MarcaId { get; set; }

    public bool SoloActivos { get; set; }

    /// <summary>Filtra por semáforo de existencias.</summary>
    public EstadoStock? Estado { get; set; }

    public int Pagina { get; set; } = 1;

    public int TamanoPagina { get; set; } = 25;

    public string OrdenarPor { get; set; } = nameof(ProductoListadoDto.Nombre);

    public bool Descendente { get; set; }
}
