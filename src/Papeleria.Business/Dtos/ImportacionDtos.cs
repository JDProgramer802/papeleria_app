namespace Papeleria.Business.Dtos;

/// <summary>Qué se va a hacer con una fila de la hoja.</summary>
public enum AccionImportacion
{
    /// <summary>El código no existe: se crea el producto.</summary>
    Crear = 1,

    /// <summary>El código ya existe: se actualizan sus datos.</summary>
    Actualizar = 2,

    /// <summary>La fila tiene algo mal y se deja fuera.</summary>
    Descartar = 3
}

/// <summary>Una fila de la hoja, ya leída y revisada.</summary>
public class FilaImportacion
{
    /// <summary>Número de fila en la hoja, para que el usuario sepa cuál corregir.</summary>
    public int Numero { get; init; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string? CodigoBarras { get; set; }

    public string Categoria { get; set; } = string.Empty;

    public string? Marca { get; set; }

    public string? Unidad { get; set; }

    public decimal Costo { get; set; }

    public decimal PrecioVenta { get; set; }

    public decimal PorcentajeIva { get; set; }

    public decimal StockActual { get; set; }

    public decimal StockMinimo { get; set; }

    public bool EsServicio { get; set; }

    public AccionImportacion Accion { get; set; } = AccionImportacion.Crear;

    /// <summary>Por qué se descarta, en palabras que sirvan para arreglarlo.</summary>
    public string? Motivo { get; set; }

    public bool Sirve => Accion != AccionImportacion.Descartar;

    public string AccionTexto => Accion switch
    {
        AccionImportacion.Crear => "Nuevo",
        AccionImportacion.Actualizar => "Actualiza",
        _ => "Se descarta"
    };
}

/// <summary>Lo que se encontró en la hoja, antes de tocar nada.</summary>
public class PrevisualizacionImportacion
{
    public string Archivo { get; init; } = string.Empty;

    public List<FilaImportacion> Filas { get; init; } = new();

    /// <summary>Problema que impide siquiera leer la hoja.</summary>
    public string? Error { get; init; }

    public bool SePuedeImportar => Error is null && Filas.Any(f => f.Sirve);

    public int Nuevos => Filas.Count(f => f.Accion == AccionImportacion.Crear);

    public int Actualizados => Filas.Count(f => f.Accion == AccionImportacion.Actualizar);

    public int Descartados => Filas.Count(f => f.Accion == AccionImportacion.Descartar);

    public string Resumen => Error is not null
        ? Error
        : $"{Nuevos} productos nuevos · {Actualizados} se actualizan · {Descartados} se descartan";
}

/// <summary>Cómo terminó la importación.</summary>
public class ResultadoImportacion
{
    public int Creados { get; init; }

    public int Actualizados { get; init; }

    /// <summary>Categorías, marcas y unidades que hubo que crear sobre la marcha.</summary>
    public int CatalogosCreados { get; init; }

    public string Resumen =>
        $"Se crearon {Creados} productos y se actualizaron {Actualizados}." +
        (CatalogosCreados > 0
            ? $" Además se crearon {CatalogosCreados} categorías, marcas o unidades que no existían."
            : string.Empty);
}
