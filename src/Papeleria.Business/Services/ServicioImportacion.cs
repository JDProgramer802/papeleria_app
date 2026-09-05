using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Papeleria.Business.Dtos;
using Papeleria.Business.Security;
using Papeleria.Data.Repositories;
using Papeleria.Data.Storage;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Entities;
using Papeleria.Domain.Enums;
using Papeleria.Domain.Exceptions;

namespace Papeleria.Business.Services;

/// <inheritdoc cref="IServicioImportacion" />
public class ServicioImportacion : IServicioImportacion
{
    /// <summary>
    /// Encabezados que se buscan en la primera fila. Se admite más de un nombre para
    /// cada columna porque la hoja rara vez llega escrita como uno esperaría.
    /// </summary>
    private static readonly Dictionary<string, string[]> Columnas = new()
    {
        ["codigo"] = new[] { "codigo", "código", "cod", "referencia", "ref" },
        ["nombre"] = new[] { "nombre", "producto", "descripcion", "descripción", "articulo", "artículo" },
        ["barras"] = new[] { "codigo de barras", "código de barras", "barras", "ean", "codigobarras" },
        ["categoria"] = new[] { "categoria", "categoría", "linea", "línea", "grupo" },
        ["marca"] = new[] { "marca", "fabricante" },
        ["unidad"] = new[] { "unidad", "unidad de medida", "medida", "und" },
        ["costo"] = new[] { "costo", "precio de compra", "compra", "costo unitario" },
        ["precio"] = new[] { "precio", "precio de venta", "venta", "precio venta", "pvp" },
        ["iva"] = new[] { "iva", "impuesto", "% iva", "porcentaje iva" },
        ["stock"] = new[] { "stock", "existencias", "cantidad", "inventario" },
        ["minimo"] = new[] { "minimo", "mínimo", "stock minimo", "stock mínimo", "punto de reorden" },
        ["tipo"] = new[] { "tipo", "es servicio", "servicio" }
    };

    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IContextoSesion _sesion;
    private readonly ILogger<ServicioImportacion> _log;

    public ServicioImportacion(
        IUnidadDeTrabajoFactory fabrica, IContextoSesion sesion, ILogger<ServicioImportacion> log)
    {
        _fabrica = fabrica;
        _sesion = sesion;
        _log = log;
    }

    // ── Lectura ─────────────────────────────────────────────────────────────

    public async Task<PrevisualizacionImportacion> PrevisualizarAsync(
        string archivo, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Productos, AccionPermiso.Crear);

        if (!File.Exists(archivo))
        {
            return new PrevisualizacionImportacion { Error = "El archivo ya no existe." };
        }

        List<FilaImportacion> filas;

        try
        {
            filas = await Task.Run(() => Leer(archivo), ct).ConfigureAwait(false);
        }
        catch (NegocioException ex)
        {
            return new PrevisualizacionImportacion { Archivo = archivo, Error = ex.Message };
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo leer la hoja de importación {Archivo}", archivo);

            return new PrevisualizacionImportacion
            {
                Archivo = archivo,
                Error = "No se pudo leer el archivo. Compruebe que sea una hoja de Excel " +
                        "(.xlsx) y que no esté abierta en otro programa."
            };
        }

        if (filas.Count == 0)
        {
            return new PrevisualizacionImportacion
            {
                Archivo = archivo,
                Error = "La hoja no tiene ninguna fila con datos debajo de los encabezados."
            };
        }

        // Se marca cuáles ya existen para poder decir si se crean o se actualizan.
        await using var unidad = _fabrica.Crear();

        var codigos = filas.Where(f => f.Sirve).Select(f => f.Codigo).ToList();

        var existentes = await unidad.Contexto.Productos
            .AsNoTracking()
            .Where(p => codigos.Contains(p.Codigo))
            .Select(p => p.Codigo)
            .ToListAsync(ct).ConfigureAwait(false);

        var yaEstan = existentes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var fila in filas.Where(f => f.Sirve))
        {
            fila.Accion = yaEstan.Contains(fila.Codigo)
                ? AccionImportacion.Actualizar
                : AccionImportacion.Crear;
        }

        return new PrevisualizacionImportacion { Archivo = archivo, Filas = filas };
    }

    private static List<FilaImportacion> Leer(string archivo)
    {
        using var libro = new XLWorkbook(archivo);
        var hoja = libro.Worksheets.FirstOrDefault()
                   ?? throw new NegocioException("El archivo no tiene ninguna hoja.");

        var usado = hoja.RangeUsed();

        if (usado is null)
        {
            throw new NegocioException("La hoja está vacía.");
        }

        var mapa = MapearEncabezados(hoja, usado);

        foreach (var obligatoria in new[] { "codigo", "nombre", "precio" })
        {
            if (!mapa.ContainsKey(obligatoria))
            {
                throw new NegocioException(
                    "No se encontró la columna «" + obligatoria + "» en la primera fila. " +
                    "Descargue la plantilla para ver los encabezados que se esperan.");
            }
        }

        var filas = new List<FilaImportacion>();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fila in usado.RowsUsed().Skip(1))
        {
            var numero = fila.RowNumber();

            string Texto(string clave) => mapa.TryGetValue(clave, out var col)
                ? fila.Cell(col).GetValue<string>().Trim()
                : string.Empty;

            var codigo = Texto("codigo");
            var nombre = Texto("nombre");

            // Una fila totalmente vacía es el final de la hoja, no un error.
            if (string.IsNullOrWhiteSpace(codigo) && string.IsNullOrWhiteSpace(nombre))
            {
                continue;
            }

            var registro = new FilaImportacion
            {
                Numero = numero,
                Codigo = codigo,
                Nombre = nombre,
                CodigoBarras = Texto("barras"),
                Categoria = Texto("categoria"),
                Marca = Texto("marca"),
                Unidad = Texto("unidad"),
                Costo = ANumero(Texto("costo")),
                PrecioVenta = ANumero(Texto("precio")),
                PorcentajeIva = ANumero(Texto("iva")),
                StockActual = ANumero(Texto("stock")),
                StockMinimo = ANumero(Texto("minimo")),
                EsServicio = EsAfirmativo(Texto("tipo"))
            };

            registro.Motivo = Revisar(registro, vistos);

            if (registro.Motivo is not null)
            {
                registro.Accion = AccionImportacion.Descartar;
            }
            else
            {
                vistos.Add(registro.Codigo);
            }

            filas.Add(registro);
        }

        return filas;
    }

    private static Dictionary<string, int> MapearEncabezados(IXLWorksheet hoja, IXLRange usado)
    {
        var mapa = new Dictionary<string, int>();
        var primera = usado.FirstRow().RowNumber();

        foreach (var celda in hoja.Row(primera).CellsUsed())
        {
            var titulo = celda.GetValue<string>().Trim().ToLowerInvariant();

            foreach (var (clave, alias) in Columnas)
            {
                if (!mapa.ContainsKey(clave) && alias.Contains(titulo))
                {
                    mapa[clave] = celda.Address.ColumnNumber;
                    break;
                }
            }
        }

        return mapa;
    }

    /// <summary>Devuelve el motivo por el que la fila no sirve, o nulo si está bien.</summary>
    private static string? Revisar(FilaImportacion fila, HashSet<string> vistos)
    {
        if (string.IsNullOrWhiteSpace(fila.Codigo))
        {
            return "Sin código";
        }

        if (string.IsNullOrWhiteSpace(fila.Nombre))
        {
            return "Sin nombre";
        }

        if (vistos.Contains(fila.Codigo))
        {
            return $"El código «{fila.Codigo}» está repetido en la hoja";
        }

        if (fila.PrecioVenta <= 0)
        {
            return "El precio de venta tiene que ser mayor que cero";
        }

        if (fila.Costo < 0 || fila.StockActual < 0 || fila.StockMinimo < 0)
        {
            return "Hay cantidades negativas";
        }

        if (fila.PorcentajeIva is < 0 or > 100)
        {
            return "El IVA tiene que estar entre 0 y 100";
        }

        return null;
    }

    /// <summary>
    /// Convierte el texto de una celda a número aguantando lo que llega de verdad:
    /// separador de miles con punto o con coma, símbolo de peso y espacios.
    /// </summary>
    private static decimal ANumero(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return 0;
        }

        var limpio = new string(texto.Where(c => char.IsDigit(c) || c is '.' or ',' or '-').ToArray());

        if (limpio.Length == 0)
        {
            return 0;
        }

        // Si trae los dos separadores, el último es el decimal.
        var punto = limpio.LastIndexOf('.');
        var coma = limpio.LastIndexOf(',');

        if (punto >= 0 && coma >= 0)
        {
            limpio = punto > coma
                ? limpio.Replace(",", string.Empty)
                : limpio.Replace(".", string.Empty).Replace(',', '.');
        }
        else if (coma >= 0)
        {
            // Una coma sola: decimal si deja dos dígitos o menos detrás, miles si no.
            limpio = limpio.Length - coma - 1 <= 2
                ? limpio.Replace(',', '.')
                : limpio.Replace(",", string.Empty);
        }
        else if (punto >= 0 && limpio.Length - punto - 1 == 3 && limpio.IndexOf('.') == punto)
        {
            // «12.000» en Colombia son doce mil, no doce.
            limpio = limpio.Replace(".", string.Empty);
        }

        return decimal.TryParse(limpio, NumberStyles.Any, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : 0;
    }

    private static bool EsAfirmativo(string texto) =>
        texto.Trim().ToLowerInvariant() is "si" or "sí" or "s" or "x" or "true" or "1" or "servicio";

    // ── Escritura ───────────────────────────────────────────────────────────

    public async Task<ResultadoImportacion> ImportarAsync(
        PrevisualizacionImportacion previsualizacion, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Productos, AccionPermiso.Crear);

        var utiles = previsualizacion.Filas.Where(f => f.Sirve).ToList();

        if (utiles.Count == 0)
        {
            throw new NegocioException("No hay ninguna fila que se pueda importar.");
        }

        await using var unidad = _fabrica.Crear();

        return await unidad.EjecutarEnTransaccionAsync(async token =>
        {
            var categorias = await unidad.Contexto.Categorias
                .ToDictionaryAsync(c => c.Nombre.ToLower(), token).ConfigureAwait(false);
            var marcas = await unidad.Contexto.Marcas
                .ToDictionaryAsync(m => m.Nombre.ToLower(), token).ConfigureAwait(false);
            var unidades = await unidad.Contexto.UnidadesMedida
                .ToDictionaryAsync(u => u.Nombre.ToLower(), token).ConfigureAwait(false);

            var codigos = utiles.Select(f => f.Codigo).ToList();

            var productos = await unidad.Contexto.Productos
                .Where(p => codigos.Contains(p.Codigo))
                .ToDictionaryAsync(p => p.Codigo.ToLower(), token).ConfigureAwait(false);

            var catalogosCreados = 0;
            int creados = 0, actualizados = 0;

            foreach (var fila in utiles)
            {
                // Las categorías, marcas y unidades que la hoja nombre y no existan se
                // crean sobre la marcha: obligar a darlas de alta antes convertiría la
                // importación en un trámite de dos pasos.
                var categoria = Resolver(unidad, categorias, fila.Categoria, "General",
                    nombre => new Categoria { Nombre = nombre }, ref catalogosCreados);

                var unidadMedida = Resolver(unidad, unidades, fila.Unidad, "Unidad",
                    nombre => new UnidadMedida
                    {
                        Nombre = nombre,
                        Abreviatura = nombre.Length > 4 ? nombre[..3].ToLower() : nombre.ToLower()
                    }, ref catalogosCreados);

                Marca? marca = null;

                if (!string.IsNullOrWhiteSpace(fila.Marca))
                {
                    marca = Resolver(unidad, marcas, fila.Marca, fila.Marca,
                        nombre => new Marca { Nombre = nombre }, ref catalogosCreados);
                }

                await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

                if (productos.TryGetValue(fila.Codigo.ToLower(), out var producto))
                {
                    // En una actualización no se toca la existencia: el inventario se
                    // mueve por el kardex, no por una hoja de cálculo.
                    producto.Nombre = fila.Nombre;
                    producto.CodigoBarras = Vacio(fila.CodigoBarras);
                    producto.CategoriaId = categoria.Id;
                    producto.MarcaId = marca?.Id;
                    producto.UnidadMedidaId = unidadMedida.Id;
                    producto.Costo = fila.Costo;
                    producto.PrecioVenta = fila.PrecioVenta;
                    producto.PorcentajeIva = fila.PorcentajeIva;
                    producto.StockMinimo = fila.StockMinimo;

                    actualizados++;
                    continue;
                }

                unidad.Contexto.Productos.Add(new Producto
                {
                    Codigo = fila.Codigo,
                    Nombre = fila.Nombre,
                    CodigoBarras = Vacio(fila.CodigoBarras),
                    CategoriaId = categoria.Id,
                    MarcaId = marca?.Id,
                    UnidadMedidaId = unidadMedida.Id,
                    Tipo = fila.EsServicio ? TipoProducto.Servicio : TipoProducto.Producto,
                    Costo = fila.Costo,
                    PrecioVenta = fila.PrecioVenta,
                    PorcentajeIva = fila.PorcentajeIva,
                    StockActual = fila.EsServicio ? 0 : fila.StockActual,
                    StockMinimo = fila.StockMinimo,
                    Activo = true
                });

                creados++;
            }

            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            _log.LogInformation(
                "Importación: {Creados} productos nuevos, {Actualizados} actualizados",
                creados, actualizados);

            return new ResultadoImportacion
            {
                Creados = creados,
                Actualizados = actualizados,
                CatalogosCreados = catalogosCreados
            };
        }, ct).ConfigureAwait(false);
    }

    private static string? Vacio(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    /// <summary>Busca el catálogo por nombre y lo crea si no está.</summary>
    private static T Resolver<T>(
        IUnidadDeTrabajo unidad, Dictionary<string, T> conocidos, string? nombre,
        string porDefecto, Func<string, T> crear, ref int creados) where T : class
    {
        var buscado = (string.IsNullOrWhiteSpace(nombre) ? porDefecto : nombre.Trim());
        var clave = buscado.ToLower();

        if (conocidos.TryGetValue(clave, out var existente))
        {
            return existente;
        }

        var nuevo = crear(buscado);
        unidad.Contexto.Add(nuevo);
        conocidos[clave] = nuevo;
        creados++;

        return nuevo;
    }

    // ── Plantilla ───────────────────────────────────────────────────────────

    public async Task<string> GenerarPlantillaAsync(
        string? rutaDestino = null, CancellationToken ct = default)
    {
        var ruta = rutaDestino ?? RutasAplicacion.RutaTemporal(".xlsx");

        await Task.Run(() =>
        {
            using var libro = new XLWorkbook();
            var hoja = libro.Worksheets.Add("Productos");

            var titulos = new[]
            {
                "Código", "Nombre", "Código de barras", "Categoría", "Marca", "Unidad",
                "Costo", "Precio de venta", "IVA", "Stock", "Stock mínimo", "Es servicio"
            };

            for (var i = 0; i < titulos.Length; i++)
            {
                var celda = hoja.Cell(1, i + 1);
                celda.Value = titulos[i];
                celda.Style.Font.Bold = true;
                celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#0379EE");
                celda.Style.Font.FontColor = XLColor.White;
            }

            object[,] ejemplos =
            {
                { "CUA-001", "Cuaderno cosido 100 hojas", "7701234000011", "Cuadernos", "Norma",
                  "Unidad", 3200, 5500, 0, 148, 30, "" },
                { "LAP-014", "Lápiz negro nº 2", "7701234000028", "Escritura", "Mirado",
                  "Unidad", 380, 800, 19, 640, 100, "" },
                { "FOT-001", "Fotocopia blanco y negro", "", "Servicios", "",
                  "Unidad", 60, 150, 0, 0, 0, "Sí" }
            };

            for (var f = 0; f < ejemplos.GetLength(0); f++)
            {
                for (var c = 0; c < ejemplos.GetLength(1); c++)
                {
                    hoja.Cell(f + 2, c + 1).Value = XLCellValue.FromObject(ejemplos[f, c]);
                }
            }

            hoja.Columns().AdjustToContents();
            hoja.SheetView.FreezeRows(1);

            var nota = hoja.Cell(ejemplos.GetLength(0) + 3, 1);
            nota.Value = "Borre estas filas de ejemplo y ponga las suyas. Obligatorias: Código, " +
                         "Nombre y Precio de venta. En «Es servicio» escriba Sí para fotocopias, " +
                         "impresiones y demás cosas que no descuentan inventario.";
            nota.Style.Font.Italic = true;
            nota.Style.Font.FontColor = XLColor.Gray;

            libro.SaveAs(ruta);
        }, ct).ConfigureAwait(false);

        return ruta;
    }
}
