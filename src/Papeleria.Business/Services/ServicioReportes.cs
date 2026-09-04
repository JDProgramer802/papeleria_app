using Microsoft.EntityFrameworkCore;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;
using Papeleria.Business.Security;
using Papeleria.Data.Repositories;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Enums;

namespace Papeleria.Business.Services;

/// <inheritdoc cref="IServicioReportes" />
public class ServicioReportes : IServicioReportes
{
    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IServicioCartera _cartera;
    private readonly IContextoSesion _sesion;

    public ServicioReportes(
        IUnidadDeTrabajoFactory fabrica, IServicioCartera cartera, IContextoSesion sesion)
    {
        _fabrica = fabrica;
        _cartera = cartera;
        _sesion = sesion;
    }

    public IReadOnlyList<DefinicionReporte> Catalogo { get; } = new List<DefinicionReporte>
    {
        new(TipoReporte.InventarioValorizado, "Inventario valorizado",
            "Existencias actuales con su valor a costo y a precio de venta.", "PackageVariantClosed", false),
        new(TipoReporte.ProductosBajoStock, "Productos con poco stock",
            "Artículos cuyas existencias están en el mínimo o por debajo.", "AlertOutline", false),
        new(TipoReporte.ProductosAgotados, "Productos agotados",
            "Artículos activos sin existencias disponibles.", "CloseCircleOutline", false),
        new(TipoReporte.ProductosMasVendidos, "Productos más vendidos",
            "Ranking de rotación por cantidad e importe facturado.", "TrendingUp", true),
        new(TipoReporte.Ventas, "Ventas",
            "Facturas emitidas en el periodo, con su medio de pago y estado.", "CashRegister", true),
        new(TipoReporte.Compras, "Compras",
            "Compras registradas a proveedores en el periodo.", "TruckDelivery", true),
        new(TipoReporte.Ganancias, "Ganancias por producto",
            "Ingresos, costo y utilidad de cada producto vendido.", "ChartLine", true),
        new(TipoReporte.Clientes, "Clientes",
            "Directorio de clientes con su histórico de compras.", "AccountGroup", false),
        new(TipoReporte.Proveedores, "Proveedores",
            "Directorio de proveedores con su histórico de compras.", "Domain", false),
        new(TipoReporte.Caja, "Caja",
            "Sesiones de caja con su arqueo y diferencias.", "CashMultiple", true),
        new(TipoReporte.Kardex, "Kardex",
            "Movimientos de inventario registrados en el periodo.", "SwapHorizontal", true),
        new(TipoReporte.Cartera, "Cartera por cobrar",
            "Clientes que deben, con la antigüedad de su deuda.", "AccountCashOutline", false)
    };

    public async Task<ReporteTabular> GenerarAsync(ParametrosReporte parametros, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Reportes, AccionPermiso.Ver);

        var reporte = await GenerarSegunTipoAsync(parametros, ct).ConfigureAwait(false);

        AplicarLimite(reporte, parametros.LimiteFilas);

        return reporte;
    }

    /// <summary>
    /// Recorta el resultado al máximo configurado y lo deja dicho. Las consultas piden
    /// una fila de más justo para poder distinguir «cabe entero» de «hay más».
    /// </summary>
    private static void AplicarLimite(ReporteTabular reporte, int limite)
    {
        if (limite <= 0 || reporte.Filas.Count <= limite)
        {
            return;
        }

        reporte.Filas = reporte.Filas.Take(limite).ToList();

        reporte.Advertencia =
            $"El reporte supera el máximo de {Formatos.Entero(limite)} filas y se muestra recortado. " +
            "Acote el periodo o los filtros para ver la información completa.";
    }

    private Task<ReporteTabular> GenerarSegunTipoAsync(ParametrosReporte parametros, CancellationToken ct)
    {
        return parametros.Tipo switch
        {
            TipoReporte.InventarioValorizado => GenerarInventarioAsync(parametros, ct),
            TipoReporte.ProductosBajoStock => GenerarBajoStockAsync(parametros, ct),
            TipoReporte.ProductosAgotados => GenerarAgotadosAsync(parametros, ct),
            TipoReporte.ProductosMasVendidos => GenerarMasVendidosAsync(parametros, ct),
            TipoReporte.Ventas => GenerarVentasAsync(parametros, ct),
            TipoReporte.Compras => GenerarComprasAsync(parametros, ct),
            TipoReporte.Ganancias => GenerarGananciasAsync(parametros, ct),
            TipoReporte.Clientes => GenerarClientesAsync(parametros, ct),
            TipoReporte.Proveedores => GenerarProveedoresAsync(parametros, ct),
            TipoReporte.Caja => GenerarCajaAsync(parametros, ct),
            TipoReporte.Cartera => GenerarCarteraAsync(parametros, ct),
            _ => GenerarKardexAsync(parametros, ct)
        };
    }

    private DefinicionReporte Definicion(TipoReporte tipo) => Catalogo.First(d => d.Tipo == tipo);

    private string DescribirPeriodo(ParametrosReporte parametros) =>
        $"{Formatos.Fecha(parametros.Desde)} — {Formatos.Fecha(parametros.Hasta)}";

    // ── Inventario ──────────────────────────────────────────────────────────

    private async Task<ReporteTabular> GenerarInventarioAsync(ParametrosReporte parametros, CancellationToken ct)
    {
        await using var unidad = _fabrica.Crear();

        // Solo mercancía: un servicio no tiene existencias que valorizar.
        var consulta = unidad.Contexto.Productos
            .AsNoTracking()
            .Where(p => p.Activo && p.Tipo == TipoProducto.Producto);

        if (parametros.CategoriaId is > 0)
        {
            consulta = consulta.Where(p => p.CategoriaId == parametros.CategoriaId);
        }

        var datos = await consulta
            .OrderBy(p => p.Categoria!.Nombre).ThenBy(p => p.Nombre)
            .Take(parametros.LimiteFilas + 1)
            .Select(p => new
            {
                p.Codigo,
                p.Nombre,
                Categoria = p.Categoria!.Nombre,
                Marca = p.Marca != null ? p.Marca.Nombre : string.Empty,
                Unidad = p.UnidadMedida!.Abreviatura,
                p.StockActual,
                p.StockMinimo,
                p.Costo,
                p.PrecioVenta
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var filas = datos.Select(p => new object?[]
        {
            p.Codigo, p.Nombre, p.Categoria, p.Marca, p.Unidad,
            p.StockActual, p.StockMinimo, p.Costo,
            Dinero.Redondear(p.StockActual * p.Costo),
            p.PrecioVenta,
            Dinero.Redondear(p.StockActual * p.PrecioVenta)
        }).ToList();

        var valorCosto = datos.Sum(p => Dinero.Redondear(p.StockActual * p.Costo));
        var valorVenta = datos.Sum(p => Dinero.Redondear(p.StockActual * p.PrecioVenta));

        return new ReporteTabular
        {
            Titulo = Definicion(TipoReporte.InventarioValorizado).Nombre,
            Subtitulo = "Existencias activas valoradas a costo y a precio de venta",
            GeneradoPor = _sesion.Usuario?.NombreCompleto ?? string.Empty,
            Columnas = new[]
            {
                new ColumnaReporte { Titulo = "Código", Ancho = 1.1f },
                new ColumnaReporte { Titulo = "Producto", Ancho = 3.2f },
                new ColumnaReporte { Titulo = "Categoría", Ancho = 1.8f },
                new ColumnaReporte { Titulo = "Marca", Ancho = 1.4f },
                new ColumnaReporte { Titulo = "Unidad", Ancho = 0.8f },
                new ColumnaReporte { Titulo = "Stock", Tipo = TipoColumna.Decimal, Ancho = 0.9f },
                new ColumnaReporte { Titulo = "Mínimo", Tipo = TipoColumna.Decimal, Ancho = 0.9f },
                new ColumnaReporte { Titulo = "Costo", Tipo = TipoColumna.Moneda, Ancho = 1.1f },
                new ColumnaReporte { Titulo = "Valor costo", Tipo = TipoColumna.Moneda, Ancho = 1.3f, Totalizar = true },
                new ColumnaReporte { Titulo = "P. venta", Tipo = TipoColumna.Moneda, Ancho = 1.1f },
                new ColumnaReporte { Titulo = "Valor venta", Tipo = TipoColumna.Moneda, Ancho = 1.3f, Totalizar = true }
            },
            Filas = filas,
            Indicadores = new[]
            {
                new IndicadorReporte("Productos", Formatos.Entero(datos.Count)),
                new IndicadorReporte("Valor a costo", Formatos.Moneda(valorCosto)),
                new IndicadorReporte("Valor a venta", Formatos.Moneda(valorVenta)),
                new IndicadorReporte("Utilidad potencial", Formatos.Moneda(valorVenta - valorCosto))
            },
            MensajeVacio = "No hay productos activos que cumplan los criterios."
        };
    }

    private Task<ReporteTabular> GenerarBajoStockAsync(ParametrosReporte parametros, CancellationToken ct) =>
        GenerarAlertaStockAsync(parametros, TipoReporte.ProductosBajoStock, ct);

    private Task<ReporteTabular> GenerarAgotadosAsync(ParametrosReporte parametros, CancellationToken ct) =>
        GenerarAlertaStockAsync(parametros, TipoReporte.ProductosAgotados, ct);

    private async Task<ReporteTabular> GenerarAlertaStockAsync(
        ParametrosReporte parametros, TipoReporte tipo, CancellationToken ct)
    {
        await using var unidad = _fabrica.Crear();

        // Solo mercancía. Un servicio siempre está en cero y sin esta condición el
        // informe de agotados salía encabezado por las fotocopias, con su cantidad
        // «sugerida» de reposición y todo.
        var consulta = unidad.Contexto.Productos.AsNoTracking()
            .Where(p => p.Activo && p.Tipo == TipoProducto.Producto);

        consulta = tipo == TipoReporte.ProductosAgotados
            ? consulta.Where(p => p.StockActual <= 0)
            : consulta.Where(p => p.StockActual > 0 && p.StockActual <= p.StockMinimo);

        if (parametros.CategoriaId is > 0)
        {
            consulta = consulta.Where(p => p.CategoriaId == parametros.CategoriaId);
        }

        var datos = await consulta
            .OrderBy(p => p.StockActual).ThenBy(p => p.Nombre)
            .Take(parametros.LimiteFilas + 1)
            .Select(p => new
            {
                p.Codigo,
                p.Nombre,
                Categoria = p.Categoria!.Nombre,
                Unidad = p.UnidadMedida!.Abreviatura,
                p.StockActual,
                p.StockMinimo,
                p.StockMaximo,
                p.Costo
            })
            .ToListAsync(ct).ConfigureAwait(false);

        // Sugerencia de reposición: llevar el producto hasta su máximo, o al doble del
        // mínimo cuando no se definió un máximo.
        var filas = datos.Select(p =>
        {
            var objetivo = p.StockMaximo > 0 ? p.StockMaximo : Math.Max(p.StockMinimo * 2, 1);
            var sugerido = Math.Max(objetivo - p.StockActual, 0);

            return new object?[]
            {
                p.Codigo, p.Nombre, p.Categoria, p.Unidad,
                p.StockActual, p.StockMinimo, p.StockMaximo,
                sugerido, Dinero.Redondear(sugerido * p.Costo)
            };
        }).ToList();

        var definicion = Definicion(tipo);

        return new ReporteTabular
        {
            Titulo = definicion.Nombre,
            Subtitulo = definicion.Descripcion,
            GeneradoPor = _sesion.Usuario?.NombreCompleto ?? string.Empty,
            Columnas = new[]
            {
                new ColumnaReporte { Titulo = "Código", Ancho = 1.1f },
                new ColumnaReporte { Titulo = "Producto", Ancho = 3.2f },
                new ColumnaReporte { Titulo = "Categoría", Ancho = 1.8f },
                new ColumnaReporte { Titulo = "Unidad", Ancho = 0.8f },
                new ColumnaReporte { Titulo = "Stock", Tipo = TipoColumna.Decimal, Ancho = 0.9f },
                new ColumnaReporte { Titulo = "Mínimo", Tipo = TipoColumna.Decimal, Ancho = 0.9f },
                new ColumnaReporte { Titulo = "Máximo", Tipo = TipoColumna.Decimal, Ancho = 0.9f },
                new ColumnaReporte { Titulo = "Sugerido", Tipo = TipoColumna.Decimal, Ancho = 1f, Totalizar = true },
                new ColumnaReporte { Titulo = "Inversión", Tipo = TipoColumna.Moneda, Ancho = 1.3f, Totalizar = true }
            },
            Filas = filas,
            Indicadores = new[]
            {
                new IndicadorReporte("Productos", Formatos.Entero(datos.Count)),
                new IndicadorReporte("Inversión estimada",
                    Formatos.Moneda(filas.Sum(f => (decimal)(f[8] ?? 0m))))
            },
            MensajeVacio = tipo == TipoReporte.ProductosAgotados
                ? "No hay productos agotados. El inventario está cubierto."
                : "Ningún producto está por debajo de su stock mínimo."
        };
    }

    // ── Ventas y rotación ───────────────────────────────────────────────────

    private async Task<ReporteTabular> GenerarMasVendidosAsync(ParametrosReporte parametros, CancellationToken ct)
    {
        await using var unidad = _fabrica.Crear();

        var (inicio, fin) = Rango(parametros);

        var datos = await unidad.Contexto.VentaDetalles
            .AsNoTracking()
            .Where(d => d.Venta!.Estado == EstadoVenta.Completada &&
                        d.Venta!.Fecha >= inicio && d.Venta!.Fecha < fin)
            .GroupBy(d => new { d.ProductoId, d.Producto!.Codigo, d.Producto!.Nombre })
            .Select(g => new
            {
                g.Key.Codigo,
                g.Key.Nombre,
                Cantidad = g.Sum(d => (double)d.Cantidad),
                Ingresos = g.Sum(d => (double)(d.Subtotal - d.ValorDescuento)),
                Costo = g.Sum(d => (double)(d.Cantidad * d.CostoUnitario)),
                Facturas = g.Select(d => d.VentaId).Distinct().Count()
            })
            .OrderByDescending(g => g.Cantidad)
            .Take(parametros.LimiteFilas + 1)
            .ToListAsync(ct).ConfigureAwait(false);

        var totalIngresos = datos.Sum(d => d.Ingresos);

        var filas = datos.Select(d =>
        {
            var ingresos = Dinero.Redondear(d.Ingresos);
            var utilidad = Dinero.Redondear(d.Ingresos - d.Costo);

            return new object?[]
            {
                d.Codigo, d.Nombre, Dinero.Redondear(d.Cantidad), d.Facturas,
                ingresos, utilidad,
                ingresos == 0 ? 0m : Math.Round(utilidad / ingresos * 100m, 1),
                totalIngresos == 0 ? 0m : Math.Round((decimal)(d.Ingresos / totalIngresos) * 100m, 1)
            };
        }).ToList();

        return new ReporteTabular
        {
            Titulo = Definicion(TipoReporte.ProductosMasVendidos).Nombre,
            Subtitulo = "Ordenado por unidades vendidas",
            Periodo = DescribirPeriodo(parametros),
            GeneradoPor = _sesion.Usuario?.NombreCompleto ?? string.Empty,
            Columnas = new[]
            {
                new ColumnaReporte { Titulo = "Código", Ancho = 1.1f },
                new ColumnaReporte { Titulo = "Producto", Ancho = 3.4f },
                new ColumnaReporte { Titulo = "Unidades", Tipo = TipoColumna.Decimal, Ancho = 1f, Totalizar = true },
                new ColumnaReporte { Titulo = "Facturas", Tipo = TipoColumna.Entero, Ancho = 0.9f, Totalizar = true },
                new ColumnaReporte { Titulo = "Ingresos", Tipo = TipoColumna.Moneda, Ancho = 1.3f, Totalizar = true },
                new ColumnaReporte { Titulo = "Utilidad", Tipo = TipoColumna.Moneda, Ancho = 1.3f, Totalizar = true },
                new ColumnaReporte { Titulo = "Margen", Tipo = TipoColumna.Porcentaje, Ancho = 1f },
                new ColumnaReporte { Titulo = "Participación", Tipo = TipoColumna.Porcentaje, Ancho = 1.1f }
            },
            Filas = filas,
            Indicadores = new[]
            {
                new IndicadorReporte("Productos con venta", Formatos.Entero(datos.Count)),
                new IndicadorReporte("Unidades vendidas", Formatos.Cantidad(Dinero.Redondear(datos.Sum(d => d.Cantidad)))),
                new IndicadorReporte("Ingresos", Formatos.Moneda(Dinero.Redondear(totalIngresos)))
            },
            MensajeVacio = "No se registraron ventas en el periodo seleccionado."
        };
    }

    private async Task<ReporteTabular> GenerarVentasAsync(ParametrosReporte parametros, CancellationToken ct)
    {
        await using var unidad = _fabrica.Crear();

        var (inicio, fin) = Rango(parametros);

        var consulta = unidad.Contexto.Ventas.AsNoTracking()
            .Where(v => v.Fecha >= inicio && v.Fecha < fin);

        if (parametros.ClienteId is > 0)
        {
            consulta = consulta.Where(v => v.ClienteId == parametros.ClienteId);
        }

        if (parametros.UsuarioId is > 0)
        {
            consulta = consulta.Where(v => v.UsuarioId == parametros.UsuarioId);
        }

        var datos = await consulta
            .OrderBy(v => v.Fecha)
            .Take(parametros.LimiteFilas + 1)
            .Select(v => new
            {
                v.NumeroFactura,
                v.Fecha,
                Cliente = v.Cliente!.Nombre,
                Usuario = v.Usuario!.NombreCompleto,
                Items = v.Detalles.Count,
                v.Subtotal,
                v.TotalDescuento,
                v.TotalIva,
                v.Total,
                v.CostoTotal,
                v.MetodoPago,
                v.Estado
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var filas = datos.Select(v => new object?[]
        {
            v.NumeroFactura, v.Fecha, v.Cliente, v.Usuario, v.Items,
            v.Subtotal, v.TotalDescuento, v.TotalIva, v.Total,
            Dinero.Redondear(v.Subtotal - v.TotalDescuento - v.CostoTotal),
            v.MetodoPago.Descripcion(),
            v.Estado == EstadoVenta.Anulada ? "Anulada" : "Completada"
        }).ToList();

        var completadas = datos.Where(v => v.Estado == EstadoVenta.Completada).ToList();
        var facturado = completadas.Sum(v => v.Total);
        var utilidad = completadas.Sum(v => v.Subtotal - v.TotalDescuento - v.CostoTotal);

        return new ReporteTabular
        {
            Titulo = Definicion(TipoReporte.Ventas).Nombre,
            Subtitulo = "Facturas emitidas en el periodo",
            Periodo = DescribirPeriodo(parametros),
            GeneradoPor = _sesion.Usuario?.NombreCompleto ?? string.Empty,
            Columnas = new[]
            {
                new ColumnaReporte { Titulo = "Factura", Ancho = 1.2f },
                new ColumnaReporte { Titulo = "Fecha", Tipo = TipoColumna.FechaHora, Ancho = 1.5f },
                new ColumnaReporte { Titulo = "Cliente", Ancho = 2.4f },
                new ColumnaReporte { Titulo = "Cajero", Ancho = 1.8f },
                new ColumnaReporte { Titulo = "Ítems", Tipo = TipoColumna.Entero, Ancho = 0.7f, Totalizar = true },
                new ColumnaReporte { Titulo = "Subtotal", Tipo = TipoColumna.Moneda, Ancho = 1.2f, Totalizar = true },
                new ColumnaReporte { Titulo = "Desc.", Tipo = TipoColumna.Moneda, Ancho = 1.1f, Totalizar = true },
                new ColumnaReporte { Titulo = "IVA", Tipo = TipoColumna.Moneda, Ancho = 1.1f, Totalizar = true },
                new ColumnaReporte { Titulo = "Total", Tipo = TipoColumna.Moneda, Ancho = 1.3f, Totalizar = true },
                new ColumnaReporte { Titulo = "Utilidad", Tipo = TipoColumna.Moneda, Ancho = 1.2f, Totalizar = true },
                new ColumnaReporte { Titulo = "Pago", Ancho = 1.1f },
                new ColumnaReporte { Titulo = "Estado", Ancho = 1.1f }
            },
            Filas = filas,
            Indicadores = new[]
            {
                new IndicadorReporte("Facturas", Formatos.Entero(completadas.Count)),
                new IndicadorReporte("Facturado", Formatos.Moneda(facturado)),
                new IndicadorReporte("Utilidad", Formatos.Moneda(utilidad)),
                new IndicadorReporte("Ticket promedio",
                    Formatos.Moneda(completadas.Count == 0 ? 0 : Dinero.DividirSeguro(facturado, completadas.Count)))
            },
            MensajeVacio = "No se registraron ventas en el periodo seleccionado."
        };
    }

    private async Task<ReporteTabular> GenerarGananciasAsync(ParametrosReporte parametros, CancellationToken ct)
    {
        await using var unidad = _fabrica.Crear();

        var (inicio, fin) = Rango(parametros);

        var consulta = unidad.Contexto.VentaDetalles.AsNoTracking()
            .Where(d => d.Venta!.Estado == EstadoVenta.Completada &&
                        d.Venta!.Fecha >= inicio && d.Venta!.Fecha < fin);

        if (parametros.CategoriaId is > 0)
        {
            consulta = consulta.Where(d => d.Producto!.CategoriaId == parametros.CategoriaId);
        }

        if (parametros.ProductoId is > 0)
        {
            consulta = consulta.Where(d => d.ProductoId == parametros.ProductoId);
        }

        var datos = await consulta
            .GroupBy(d => new { d.ProductoId, d.Producto!.Codigo, d.Producto!.Nombre, Categoria = d.Producto!.Categoria!.Nombre })
            .Select(g => new
            {
                g.Key.Codigo,
                g.Key.Nombre,
                g.Key.Categoria,
                Cantidad = g.Sum(d => (double)d.Cantidad),
                Ingresos = g.Sum(d => (double)(d.Subtotal - d.ValorDescuento)),
                Costo = g.Sum(d => (double)(d.Cantidad * d.CostoUnitario))
            })
            .OrderByDescending(g => g.Ingresos - g.Costo)
            .Take(parametros.LimiteFilas + 1)
            .ToListAsync(ct).ConfigureAwait(false);

        var filas = datos.Select(d =>
        {
            var ingresos = Dinero.Redondear(d.Ingresos);
            var costo = Dinero.Redondear(d.Costo);
            var utilidad = Dinero.Redondear(ingresos - costo);

            return new object?[]
            {
                d.Codigo, d.Nombre, d.Categoria, Dinero.Redondear(d.Cantidad),
                ingresos, costo, utilidad,
                ingresos == 0 ? 0m : Math.Round(utilidad / ingresos * 100m, 1)
            };
        }).ToList();

        var totalIngresos = Dinero.Redondear(datos.Sum(d => d.Ingresos));
        var totalCosto = Dinero.Redondear(datos.Sum(d => d.Costo));

        return new ReporteTabular
        {
            Titulo = Definicion(TipoReporte.Ganancias).Nombre,
            Subtitulo = "Ingresos sin IVA, costo de la mercancía vendida y utilidad bruta",
            Periodo = DescribirPeriodo(parametros),
            GeneradoPor = _sesion.Usuario?.NombreCompleto ?? string.Empty,
            Columnas = new[]
            {
                new ColumnaReporte { Titulo = "Código", Ancho = 1.1f },
                new ColumnaReporte { Titulo = "Producto", Ancho = 3.2f },
                new ColumnaReporte { Titulo = "Categoría", Ancho = 1.8f },
                new ColumnaReporte { Titulo = "Unidades", Tipo = TipoColumna.Decimal, Ancho = 1f, Totalizar = true },
                new ColumnaReporte { Titulo = "Ingresos", Tipo = TipoColumna.Moneda, Ancho = 1.3f, Totalizar = true },
                new ColumnaReporte { Titulo = "Costo", Tipo = TipoColumna.Moneda, Ancho = 1.3f, Totalizar = true },
                new ColumnaReporte { Titulo = "Utilidad", Tipo = TipoColumna.Moneda, Ancho = 1.3f, Totalizar = true },
                new ColumnaReporte { Titulo = "Margen", Tipo = TipoColumna.Porcentaje, Ancho = 1f }
            },
            Filas = filas,
            Indicadores = new[]
            {
                new IndicadorReporte("Ingresos", Formatos.Moneda(totalIngresos)),
                new IndicadorReporte("Costo", Formatos.Moneda(totalCosto)),
                new IndicadorReporte("Utilidad bruta", Formatos.Moneda(totalIngresos - totalCosto)),
                new IndicadorReporte("Margen global", totalIngresos == 0
                    ? "0,0 %"
                    : Formatos.Porcentaje(Math.Round((totalIngresos - totalCosto) / totalIngresos * 100m, 1)))
            },
            MensajeVacio = "No hay ventas en el periodo para calcular ganancias."
        };
    }

    // ── Compras ─────────────────────────────────────────────────────────────

    private async Task<ReporteTabular> GenerarComprasAsync(ParametrosReporte parametros, CancellationToken ct)
    {
        await using var unidad = _fabrica.Crear();

        var (inicio, fin) = Rango(parametros);

        var consulta = unidad.Contexto.Compras.AsNoTracking()
            .Where(c => c.Fecha >= inicio && c.Fecha < fin);

        if (parametros.ProveedorId is > 0)
        {
            consulta = consulta.Where(c => c.ProveedorId == parametros.ProveedorId);
        }

        var datos = await consulta
            .OrderBy(c => c.Fecha)
            .Take(parametros.LimiteFilas + 1)
            .Select(c => new
            {
                c.Numero,
                c.NumeroFacturaProveedor,
                c.Fecha,
                Proveedor = c.Proveedor!.Nombre,
                Usuario = c.Usuario!.NombreCompleto,
                Items = c.Detalles.Count,
                c.Subtotal,
                c.TotalDescuento,
                c.TotalIva,
                c.Total,
                c.Estado
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var filas = datos.Select(c => new object?[]
        {
            c.Numero, c.NumeroFacturaProveedor ?? "—", c.Fecha, c.Proveedor, c.Usuario,
            c.Items, c.Subtotal, c.TotalDescuento, c.TotalIva, c.Total,
            c.Estado == EstadoCompra.Anulada ? "Anulada" : "Registrada"
        }).ToList();

        var registradas = datos.Where(c => c.Estado == EstadoCompra.Registrada).ToList();

        return new ReporteTabular
        {
            Titulo = Definicion(TipoReporte.Compras).Nombre,
            Subtitulo = "Compras registradas a proveedores",
            Periodo = DescribirPeriodo(parametros),
            GeneradoPor = _sesion.Usuario?.NombreCompleto ?? string.Empty,
            Columnas = new[]
            {
                new ColumnaReporte { Titulo = "Número", Ancho = 1.2f },
                new ColumnaReporte { Titulo = "Fact. proveedor", Ancho = 1.3f },
                new ColumnaReporte { Titulo = "Fecha", Tipo = TipoColumna.Fecha, Ancho = 1.1f },
                new ColumnaReporte { Titulo = "Proveedor", Ancho = 2.6f },
                new ColumnaReporte { Titulo = "Registró", Ancho = 1.8f },
                new ColumnaReporte { Titulo = "Ítems", Tipo = TipoColumna.Entero, Ancho = 0.7f, Totalizar = true },
                new ColumnaReporte { Titulo = "Subtotal", Tipo = TipoColumna.Moneda, Ancho = 1.2f, Totalizar = true },
                new ColumnaReporte { Titulo = "Desc.", Tipo = TipoColumna.Moneda, Ancho = 1.1f, Totalizar = true },
                new ColumnaReporte { Titulo = "IVA", Tipo = TipoColumna.Moneda, Ancho = 1.1f, Totalizar = true },
                new ColumnaReporte { Titulo = "Total", Tipo = TipoColumna.Moneda, Ancho = 1.3f, Totalizar = true },
                new ColumnaReporte { Titulo = "Estado", Ancho = 1f }
            },
            Filas = filas,
            Indicadores = new[]
            {
                new IndicadorReporte("Compras", Formatos.Entero(registradas.Count)),
                new IndicadorReporte("Invertido", Formatos.Moneda(registradas.Sum(c => c.Total))),
                new IndicadorReporte("IVA pagado", Formatos.Moneda(registradas.Sum(c => c.TotalIva)))
            },
            MensajeVacio = "No se registraron compras en el periodo seleccionado."
        };
    }

    // ── Terceros ────────────────────────────────────────────────────────────

    private async Task<ReporteTabular> GenerarCarteraAsync(ParametrosReporte parametros, CancellationToken ct)
    {
        // Se reutiliza el cálculo del módulo de cartera para que el informe y la
        // pantalla no puedan dar cifras distintas.
        var pagina = await _cartera.BuscarAsync(new FiltroCartera
        {
            SoloConSaldo = true,
            Pagina = 1,
            TamanoPagina = parametros.LimiteFilas + 1
        }, ct).ConfigureAwait(false);

        var filas = pagina.Elementos
            .OrderByDescending(c => c.DiasDeMora)
            .ThenByDescending(c => c.Saldo)
            .Select(c => new object?[]
            {
                c.Nombre, c.NumeroDocumento ?? "—", c.Telefono ?? "—",
                c.FacturasPendientes, c.DeudaMasAntigua, c.DiasDeMora,
                c.LimiteCredito, c.Saldo
            })
            .ToList();

        return new ReporteTabular
        {
            Titulo = Definicion(TipoReporte.Cartera).Nombre,
            Subtitulo = "Ordenada por antigüedad: primero a quién hay que cobrar",
            GeneradoPor = _sesion.Usuario?.NombreCompleto ?? string.Empty,
            Columnas = new[]
            {
                new ColumnaReporte { Titulo = "Cliente", Ancho = 2.8f },
                new ColumnaReporte { Titulo = "Documento", Ancho = 1.4f },
                new ColumnaReporte { Titulo = "Teléfono", Ancho = 1.4f },
                new ColumnaReporte { Titulo = "Facturas", Tipo = TipoColumna.Entero, Ancho = 0.9f, Totalizar = true },
                new ColumnaReporte { Titulo = "Deuda desde", Tipo = TipoColumna.Fecha, Ancho = 1.2f },
                new ColumnaReporte { Titulo = "Días", Tipo = TipoColumna.Entero, Ancho = 0.7f },
                new ColumnaReporte { Titulo = "Cupo", Tipo = TipoColumna.Moneda, Ancho = 1.3f },
                new ColumnaReporte { Titulo = "Debe", Tipo = TipoColumna.Moneda, Ancho = 1.4f, Totalizar = true }
            },
            Filas = filas,
            MensajeVacio = "Ningún cliente tiene deudas pendientes."
        };
    }

    private async Task<ReporteTabular> GenerarClientesAsync(ParametrosReporte parametros, CancellationToken ct)
    {
        await using var unidad = _fabrica.Crear();

        var datos = await unidad.Contexto.Clientes
            .AsNoTracking()
            .Take(parametros.LimiteFilas + 1)
            .Select(c => new
            {
                c.Nombre,
                c.TipoDocumento,
                c.NumeroDocumento,
                c.Telefono,
                c.Correo,
                c.Ciudad,
                c.Activo,
                Compras = c.Ventas.Count(v => v.Estado == EstadoVenta.Completada),
                Total = c.Ventas.Where(v => v.Estado == EstadoVenta.Completada).Sum(v => (double?)v.Total) ?? 0,
                Ultima = c.Ventas.Where(v => v.Estado == EstadoVenta.Completada)
                    .Max(v => (DateTime?)v.Fecha)
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var ordenados = datos.OrderByDescending(c => c.Total).ThenBy(c => c.Nombre).ToList();

        var filas = ordenados.Select(c => new object?[]
        {
            c.Nombre, c.TipoDocumento.Descripcion(), c.NumeroDocumento ?? "—",
            c.Telefono ?? "—", c.Correo ?? "—", c.Ciudad ?? "—",
            c.Compras, Dinero.Redondear(c.Total), c.Ultima, c.Activo
        }).ToList();

        return new ReporteTabular
        {
            Titulo = Definicion(TipoReporte.Clientes).Nombre,
            Subtitulo = "Directorio con el acumulado histórico de compras",
            GeneradoPor = _sesion.Usuario?.NombreCompleto ?? string.Empty,
            Columnas = new[]
            {
                new ColumnaReporte { Titulo = "Cliente", Ancho = 2.8f },
                new ColumnaReporte { Titulo = "Tipo doc.", Ancho = 1.5f },
                new ColumnaReporte { Titulo = "Documento", Ancho = 1.4f },
                new ColumnaReporte { Titulo = "Teléfono", Ancho = 1.3f },
                new ColumnaReporte { Titulo = "Correo", Ancho = 2.2f },
                new ColumnaReporte { Titulo = "Ciudad", Ancho = 1.3f },
                new ColumnaReporte { Titulo = "Compras", Tipo = TipoColumna.Entero, Ancho = 0.9f, Totalizar = true },
                new ColumnaReporte { Titulo = "Total", Tipo = TipoColumna.Moneda, Ancho = 1.4f, Totalizar = true },
                new ColumnaReporte { Titulo = "Última", Tipo = TipoColumna.Fecha, Ancho = 1.1f },
                new ColumnaReporte { Titulo = "Activo", Tipo = TipoColumna.Booleano, Ancho = 0.8f }
            },
            Filas = filas,
            Indicadores = new[]
            {
                new IndicadorReporte("Clientes", Formatos.Entero(ordenados.Count)),
                new IndicadorReporte("Con compras", Formatos.Entero(ordenados.Count(c => c.Compras > 0))),
                new IndicadorReporte("Facturado", Formatos.Moneda(Dinero.Redondear(ordenados.Sum(c => c.Total))))
            },
            MensajeVacio = "Todavía no hay clientes registrados."
        };
    }

    private async Task<ReporteTabular> GenerarProveedoresAsync(ParametrosReporte parametros, CancellationToken ct)
    {
        await using var unidad = _fabrica.Crear();

        var datos = await unidad.Contexto.Proveedores
            .AsNoTracking()
            .Take(parametros.LimiteFilas + 1)
            .Select(p => new
            {
                p.Nombre,
                p.Nit,
                p.Contacto,
                p.Telefono,
                p.Correo,
                p.Ciudad,
                p.Activo,
                Compras = p.Compras.Count(c => c.Estado == EstadoCompra.Registrada),
                Total = p.Compras.Where(c => c.Estado == EstadoCompra.Registrada).Sum(c => (double?)c.Total) ?? 0,
                Ultima = p.Compras.Where(c => c.Estado == EstadoCompra.Registrada)
                    .Max(c => (DateTime?)c.Fecha)
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var ordenados = datos.OrderByDescending(p => p.Total).ThenBy(p => p.Nombre).ToList();

        var filas = ordenados.Select(p => new object?[]
        {
            p.Nombre, p.Nit ?? "—", p.Contacto ?? "—", p.Telefono ?? "—",
            p.Correo ?? "—", p.Ciudad ?? "—",
            p.Compras, Dinero.Redondear(p.Total), p.Ultima, p.Activo
        }).ToList();

        return new ReporteTabular
        {
            Titulo = Definicion(TipoReporte.Proveedores).Nombre,
            Subtitulo = "Directorio con el acumulado histórico de compras",
            GeneradoPor = _sesion.Usuario?.NombreCompleto ?? string.Empty,
            Columnas = new[]
            {
                new ColumnaReporte { Titulo = "Proveedor", Ancho = 2.8f },
                new ColumnaReporte { Titulo = "NIT", Ancho = 1.4f },
                new ColumnaReporte { Titulo = "Contacto", Ancho = 1.8f },
                new ColumnaReporte { Titulo = "Teléfono", Ancho = 1.3f },
                new ColumnaReporte { Titulo = "Correo", Ancho = 2.2f },
                new ColumnaReporte { Titulo = "Ciudad", Ancho = 1.3f },
                new ColumnaReporte { Titulo = "Compras", Tipo = TipoColumna.Entero, Ancho = 0.9f, Totalizar = true },
                new ColumnaReporte { Titulo = "Total", Tipo = TipoColumna.Moneda, Ancho = 1.4f, Totalizar = true },
                new ColumnaReporte { Titulo = "Última", Tipo = TipoColumna.Fecha, Ancho = 1.1f },
                new ColumnaReporte { Titulo = "Activo", Tipo = TipoColumna.Booleano, Ancho = 0.8f }
            },
            Filas = filas,
            Indicadores = new[]
            {
                new IndicadorReporte("Proveedores", Formatos.Entero(ordenados.Count)),
                new IndicadorReporte("Con compras", Formatos.Entero(ordenados.Count(p => p.Compras > 0))),
                new IndicadorReporte("Invertido", Formatos.Moneda(Dinero.Redondear(ordenados.Sum(p => p.Total))))
            },
            MensajeVacio = "Todavía no hay proveedores registrados."
        };
    }

    // ── Caja y kardex ───────────────────────────────────────────────────────

    private async Task<ReporteTabular> GenerarCajaAsync(ParametrosReporte parametros, CancellationToken ct)
    {
        await using var unidad = _fabrica.Crear();

        var (inicio, fin) = Rango(parametros);

        var datos = await unidad.Contexto.CajaSesiones
            .AsNoTracking()
            .Where(s => s.FechaApertura >= inicio && s.FechaApertura < fin)
            .OrderByDescending(s => s.FechaApertura)
            .Take(parametros.LimiteFilas + 1)
            .Select(s => new
            {
                s.Id,
                s.FechaApertura,
                s.FechaCierre,
                Apertura = s.UsuarioApertura!.NombreCompleto,
                Cierre = s.UsuarioCierre != null ? s.UsuarioCierre.NombreCompleto : "—",
                s.MontoInicial,
                s.TotalVentasEfectivo,
                s.TotalVentasOtros,
                s.TotalIngresos,
                s.TotalEgresos,
                s.MontoEsperado,
                s.MontoReal,
                s.Diferencia,
                s.CantidadVentas,
                s.Estado
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var filas = datos.Select(s => new object?[]
        {
            s.Id, s.FechaApertura, s.FechaCierre, s.Apertura, s.Cierre,
            s.MontoInicial, s.CantidadVentas, s.TotalVentasEfectivo, s.TotalVentasOtros,
            s.TotalIngresos, s.TotalEgresos, s.MontoEsperado, s.MontoReal, s.Diferencia,
            s.Estado.Descripcion()
        }).ToList();

        var cerradas = datos.Where(s => s.Estado == EstadoCajaSesion.Cerrada).ToList();

        return new ReporteTabular
        {
            Titulo = Definicion(TipoReporte.Caja).Nombre,
            Subtitulo = "Sesiones de caja con su arqueo",
            Periodo = DescribirPeriodo(parametros),
            GeneradoPor = _sesion.Usuario?.NombreCompleto ?? string.Empty,
            Columnas = new[]
            {
                new ColumnaReporte { Titulo = "N.º", Tipo = TipoColumna.Entero, Ancho = 0.6f },
                new ColumnaReporte { Titulo = "Apertura", Tipo = TipoColumna.FechaHora, Ancho = 1.5f },
                new ColumnaReporte { Titulo = "Cierre", Tipo = TipoColumna.FechaHora, Ancho = 1.5f },
                new ColumnaReporte { Titulo = "Abrió", Ancho = 1.6f },
                new ColumnaReporte { Titulo = "Cerró", Ancho = 1.6f },
                new ColumnaReporte { Titulo = "Base", Tipo = TipoColumna.Moneda, Ancho = 1.1f },
                new ColumnaReporte { Titulo = "Ventas", Tipo = TipoColumna.Entero, Ancho = 0.8f, Totalizar = true },
                new ColumnaReporte { Titulo = "Efectivo", Tipo = TipoColumna.Moneda, Ancho = 1.2f, Totalizar = true },
                new ColumnaReporte { Titulo = "Otros medios", Tipo = TipoColumna.Moneda, Ancho = 1.2f, Totalizar = true },
                new ColumnaReporte { Titulo = "Ingresos", Tipo = TipoColumna.Moneda, Ancho = 1.1f, Totalizar = true },
                new ColumnaReporte { Titulo = "Egresos", Tipo = TipoColumna.Moneda, Ancho = 1.1f, Totalizar = true },
                new ColumnaReporte { Titulo = "Esperado", Tipo = TipoColumna.Moneda, Ancho = 1.2f },
                new ColumnaReporte { Titulo = "Contado", Tipo = TipoColumna.Moneda, Ancho = 1.2f },
                new ColumnaReporte { Titulo = "Diferencia", Tipo = TipoColumna.Moneda, Ancho = 1.2f, Totalizar = true },
                new ColumnaReporte { Titulo = "Estado", Ancho = 0.9f }
            },
            Filas = filas,
            Indicadores = new[]
            {
                new IndicadorReporte("Sesiones", Formatos.Entero(datos.Count)),
                new IndicadorReporte("Ventas en efectivo", Formatos.Moneda(datos.Sum(s => s.TotalVentasEfectivo))),
                new IndicadorReporte("Descuadre acumulado", Formatos.Moneda(cerradas.Sum(s => s.Diferencia)))
            },
            MensajeVacio = "No hay sesiones de caja en el periodo seleccionado."
        };
    }

    private async Task<ReporteTabular> GenerarKardexAsync(ParametrosReporte parametros, CancellationToken ct)
    {
        await using var unidad = _fabrica.Crear();

        var (inicio, fin) = Rango(parametros);

        var consulta = unidad.Contexto.MovimientosKardex.AsNoTracking()
            .Where(m => m.Fecha >= inicio && m.Fecha < fin);

        if (parametros.ProductoId is > 0)
        {
            consulta = consulta.Where(m => m.ProductoId == parametros.ProductoId);
        }

        if (parametros.CategoriaId is > 0)
        {
            consulta = consulta.Where(m => m.Producto!.CategoriaId == parametros.CategoriaId);
        }

        if (parametros.UsuarioId is > 0)
        {
            consulta = consulta.Where(m => m.UsuarioId == parametros.UsuarioId);
        }

        var datos = await consulta
            .OrderBy(m => m.Fecha).ThenBy(m => m.Id)
            .Take(parametros.LimiteFilas + 1)
            .Select(m => new
            {
                m.Fecha,
                Codigo = m.Producto!.Codigo,
                Producto = m.Producto!.Nombre,
                m.Tipo,
                m.Entrada,
                m.Salida,
                m.StockAnterior,
                m.StockNuevo,
                m.CostoUnitario,
                m.Cantidad,
                Usuario = m.Usuario!.NombreCompleto,
                m.Motivo,
                m.DocumentoReferencia
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var filas = datos.Select(m => new object?[]
        {
            m.Fecha, m.Codigo, m.Producto, m.Tipo.Descripcion(),
            m.Entrada, m.Salida, m.StockAnterior, m.StockNuevo,
            m.CostoUnitario, Dinero.Redondear(m.Cantidad * m.CostoUnitario),
            m.Usuario, m.DocumentoReferencia ?? "—", m.Motivo
        }).ToList();

        return new ReporteTabular
        {
            Titulo = Definicion(TipoReporte.Kardex).Nombre,
            Subtitulo = "Movimientos de inventario en orden cronológico",
            Periodo = DescribirPeriodo(parametros),
            GeneradoPor = _sesion.Usuario?.NombreCompleto ?? string.Empty,
            Columnas = new[]
            {
                new ColumnaReporte { Titulo = "Fecha", Tipo = TipoColumna.FechaHora, Ancho = 1.5f },
                new ColumnaReporte { Titulo = "Código", Ancho = 1.1f },
                new ColumnaReporte { Titulo = "Producto", Ancho = 2.8f },
                new ColumnaReporte { Titulo = "Movimiento", Ancho = 1.6f },
                new ColumnaReporte { Titulo = "Entrada", Tipo = TipoColumna.Decimal, Ancho = 0.9f, Totalizar = true },
                new ColumnaReporte { Titulo = "Salida", Tipo = TipoColumna.Decimal, Ancho = 0.9f, Totalizar = true },
                new ColumnaReporte { Titulo = "Stock ant.", Tipo = TipoColumna.Decimal, Ancho = 1f },
                new ColumnaReporte { Titulo = "Stock nuevo", Tipo = TipoColumna.Decimal, Ancho = 1f },
                new ColumnaReporte { Titulo = "Costo", Tipo = TipoColumna.Moneda, Ancho = 1.1f },
                new ColumnaReporte { Titulo = "Valor", Tipo = TipoColumna.Moneda, Ancho = 1.2f, Totalizar = true },
                new ColumnaReporte { Titulo = "Usuario", Ancho = 1.6f },
                new ColumnaReporte { Titulo = "Documento", Ancho = 1.2f },
                new ColumnaReporte { Titulo = "Motivo", Ancho = 2.4f }
            },
            Filas = filas,
            Indicadores = new[]
            {
                new IndicadorReporte("Movimientos", Formatos.Entero(datos.Count)),
                new IndicadorReporte("Unidades entradas", Formatos.Cantidad(datos.Sum(m => m.Entrada))),
                new IndicadorReporte("Unidades salidas", Formatos.Cantidad(datos.Sum(m => m.Salida)))
            },
            MensajeVacio = "No hay movimientos de inventario en el periodo seleccionado."
        };
    }

    /// <summary>Normaliza el rango: incluye el día completo indicado en «hasta».</summary>
    private static (DateTime Inicio, DateTime Fin) Rango(ParametrosReporte parametros) =>
        (parametros.Desde.Date, parametros.Hasta.Date.AddDays(1));
}
