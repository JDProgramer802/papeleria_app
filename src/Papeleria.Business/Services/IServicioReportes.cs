using Papeleria.Business.Dtos;

namespace Papeleria.Business.Services;

/// <summary>Reportes disponibles en el módulo de informes.</summary>
public enum TipoReporte
{
    InventarioValorizado = 0,
    ProductosBajoStock = 1,
    ProductosAgotados = 2,
    ProductosMasVendidos = 3,
    Ventas = 4,
    Compras = 5,
    Ganancias = 6,
    Clientes = 7,
    Proveedores = 8,
    Caja = 9,
    Kardex = 10
}

/// <summary>Metadatos de un reporte, usados para poblar el selector de la interfaz.</summary>
public record DefinicionReporte(
    TipoReporte Tipo,
    string Nombre,
    string Descripcion,
    string Icono,
    bool RequierePeriodo);

/// <summary>Criterios con los que se genera un reporte.</summary>
public class ParametrosReporte
{
    public TipoReporte Tipo { get; set; }

    public DateTime Desde { get; set; } = DateTime.Today.AddDays(-30);

    public DateTime Hasta { get; set; } = DateTime.Today;

    public int? CategoriaId { get; set; }

    public int? ProductoId { get; set; }

    public int? ClienteId { get; set; }

    public int? ProveedorId { get; set; }

    public int? UsuarioId { get; set; }

    /// <summary>Número máximo de filas; protege la interfaz ante consultas muy amplias.</summary>
    public int LimiteFilas { get; set; } = 5000;
}

/// <summary>Construye los reportes del sistema en un formato neutro y exportable.</summary>
public interface IServicioReportes
{
    /// <summary>Catálogo de reportes disponibles.</summary>
    IReadOnlyList<DefinicionReporte> Catalogo { get; }

    Task<ReporteTabular> GenerarAsync(ParametrosReporte parametros, CancellationToken ct = default);
}
