using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;
using Papeleria.Data.Repositories;
using Papeleria.Domain.Enums;

namespace Papeleria.Business.Services;

/// <summary>Indicadores agregados que alimentan el panel de inicio.</summary>
public interface IServicioDashboard
{
    Task<ResumenDashboardDto> ObtenerResumenAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IServicioDashboard" />
public class ServicioDashboard : IServicioDashboard
{
    private const int MesesDeHistorial = 12;

    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IServicioVentas _ventas;
    private readonly IServicioKardex _kardex;

    public ServicioDashboard(
        IUnidadDeTrabajoFactory fabrica,
        IServicioVentas ventas,
        IServicioKardex kardex)
    {
        _fabrica = fabrica;
        _ventas = ventas;
        _kardex = kardex;
    }

    public async Task<ResumenDashboardDto> ObtenerResumenAsync(CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        var hoy = DateTime.Today;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var inicioMesAnterior = inicioMes.AddMonths(-1);
        var inicioHistorial = inicioMes.AddMonths(-(MesesDeHistorial - 1));

        var inventario = await ObtenerInventarioAsync(unidad, ct).ConfigureAwait(false);
        var ventasMes = await ObtenerTotalesVentaAsync(unidad, inicioMes, inicioMes.AddMonths(1), ct)
            .ConfigureAwait(false);
        var ventasMesAnterior = await ObtenerTotalesVentaAsync(unidad, inicioMesAnterior, inicioMes, ct)
            .ConfigureAwait(false);
        var ventasHoy = await ObtenerTotalesVentaAsync(unidad, hoy, hoy.AddDays(1), ct).ConfigureAwait(false);

        var comprasMes = await ObtenerTotalComprasAsync(unidad, inicioMes, inicioMes.AddMonths(1), ct)
            .ConfigureAwait(false);
        var comprasMesAnterior = await ObtenerTotalComprasAsync(unidad, inicioMesAnterior, inicioMes, ct)
            .ConfigureAwait(false);

        var serieVentas = await ConstruirSerieVentasAsync(unidad, inicioHistorial, ct).ConfigureAwait(false);
        var serieCompras = await ConstruirSerieComprasAsync(unidad, inicioHistorial, ct).ConfigureAwait(false);

        var topProductos = await _ventas.ObtenerMasVendidosAsync(inicioMes, hoy, 8, ct).ConfigureAwait(false);
        var movimientos = await _kardex.ObtenerRecientesAsync(12, ct).ConfigureAwait(false);

        var proveedores = await unidad.Contexto.Proveedores.CountAsync(p => p.Activo, ct).ConfigureAwait(false);
        var clientes = await unidad.Contexto.Clientes.CountAsync(c => c.Activo, ct).ConfigureAwait(false);
        var cajaAbierta = await unidad.Contexto.CajaSesiones
            .AnyAsync(s => s.Estado == EstadoCajaSesion.Abierta, ct).ConfigureAwait(false);

        var alertas = ConstruirAlertas(inventario, cajaAbierta, ventasMes.Cantidad);

        return new ResumenDashboardDto
        {
            TotalProductos = inventario.TotalProductos,
            ValorInventario = inventario.ValorCosto,
            ComprasDelMes = comprasMes,
            VentasDelMes = ventasMes.Total,
            GananciasDelMes = ventasMes.Utilidad,
            ProductosBajoStock = inventario.BajoMinimo,
            ProductosAgotados = inventario.Agotados,
            TotalProveedores = proveedores,
            TotalClientes = clientes,

            VariacionVentas = Dinero.VariacionPorcentual(ventasMes.Total, ventasMesAnterior.Total),
            VariacionCompras = Dinero.VariacionPorcentual(comprasMes, comprasMesAnterior),
            VariacionGanancias = Dinero.VariacionPorcentual(ventasMes.Utilidad, ventasMesAnterior.Utilidad),

            VentasDelDia = ventasHoy.Cantidad,
            MontoVentasDelDia = ventasHoy.Total,
            TicketPromedio = ventasMes.Cantidad == 0
                ? 0
                : Dinero.DividirSeguro(ventasMes.Total, ventasMes.Cantidad),
            CajaAbierta = cajaAbierta,

            SerieVentas = serieVentas,
            SerieCompras = serieCompras,
            ProductosMasVendidos = topProductos,
            MovimientosRecientes = movimientos,
            Alertas = alertas
        };
    }

    private sealed record TotalesInventario(
        int TotalProductos, decimal ValorCosto, int Agotados, int BajoMinimo);

    private static async Task<TotalesInventario> ObtenerInventarioAsync(
        IUnidadDeTrabajo unidad, CancellationToken ct)
    {
        // El valor del inventario y los agotados solo tienen sentido sobre mercancía.
        var datos = await unidad.Contexto.Productos
            .AsNoTracking()
            .Where(p => p.Tipo == TipoProducto.Producto)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(p => p.Activo),
                Valor = g.Sum(p => (double)(p.StockActual * p.Costo)),
                Agotados = g.Count(p => p.Activo && p.StockActual <= 0),
                Bajos = g.Count(p => p.Activo && p.StockActual > 0 && p.StockActual <= p.StockMinimo)
            })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return datos is null
            ? new TotalesInventario(0, 0, 0, 0)
            : new TotalesInventario(datos.Total, Dinero.Redondear(datos.Valor), datos.Agotados, datos.Bajos);
    }

    private sealed record TotalesVenta(int Cantidad, decimal Total, decimal Utilidad);

    private static async Task<TotalesVenta> ObtenerTotalesVentaAsync(
        IUnidadDeTrabajo unidad, DateTime desde, DateTime hasta, CancellationToken ct)
    {
        var datos = await unidad.Contexto.Ventas
            .AsNoTracking()
            .Where(v => v.Estado == EstadoVenta.Completada && v.Fecha >= desde && v.Fecha < hasta)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Cantidad = g.Count(),
                Total = g.Sum(v => (double)v.Total),
                // Utilidad bruta: base gravable menos costo de la mercancía vendida.
                Utilidad = g.Sum(v => (double)(v.Subtotal - v.TotalDescuento - v.CostoTotal))
            })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return datos is null
            ? new TotalesVenta(0, 0, 0)
            : new TotalesVenta(datos.Cantidad, Dinero.Redondear(datos.Total), Dinero.Redondear(datos.Utilidad));
    }

    private static async Task<decimal> ObtenerTotalComprasAsync(
        IUnidadDeTrabajo unidad, DateTime desde, DateTime hasta, CancellationToken ct)
    {
        var total = await unidad.Contexto.Compras
            .AsNoTracking()
            .Where(c => c.Estado == EstadoCompra.Registrada && c.Fecha >= desde && c.Fecha < hasta)
            .SumAsync(c => (double?)c.Total, ct).ConfigureAwait(false);

        return Dinero.Redondear(total ?? 0);
    }

    private static async Task<IReadOnlyList<PuntoSerie>> ConstruirSerieVentasAsync(
        IUnidadDeTrabajo unidad, DateTime desde, CancellationToken ct)
    {
        var agrupado = await unidad.Contexto.Ventas
            .AsNoTracking()
            .Where(v => v.Estado == EstadoVenta.Completada && v.Fecha >= desde)
            .GroupBy(v => new { v.Fecha.Year, v.Fecha.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(v => (double)v.Total) })
            .ToListAsync(ct).ConfigureAwait(false);

        return CompletarMeses(desde, agrupado.ToDictionary(
            a => new DateTime(a.Year, a.Month, 1), a => Dinero.Redondear(a.Total)));
    }

    private static async Task<IReadOnlyList<PuntoSerie>> ConstruirSerieComprasAsync(
        IUnidadDeTrabajo unidad, DateTime desde, CancellationToken ct)
    {
        var agrupado = await unidad.Contexto.Compras
            .AsNoTracking()
            .Where(c => c.Estado == EstadoCompra.Registrada && c.Fecha >= desde)
            .GroupBy(c => new { c.Fecha.Year, c.Fecha.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(c => (double)c.Total) })
            .ToListAsync(ct).ConfigureAwait(false);

        return CompletarMeses(desde, agrupado.ToDictionary(
            a => new DateTime(a.Year, a.Month, 1), a => Dinero.Redondear(a.Total)));
    }

    /// <summary>
    /// Rellena con cero los meses sin movimiento para que el gráfico mantenga
    /// una escala temporal continua.
    /// </summary>
    private static IReadOnlyList<PuntoSerie> CompletarMeses(
        DateTime desde, IReadOnlyDictionary<DateTime, decimal> valores)
    {
        var cultura = CultureInfo.GetCultureInfo("es-CO");
        var puntos = new List<PuntoSerie>(MesesDeHistorial);

        for (var i = 0; i < MesesDeHistorial; i++)
        {
            var periodo = desde.AddMonths(i);
            var etiqueta = cultura.DateTimeFormat.GetAbbreviatedMonthName(periodo.Month)
                .TrimEnd('.').ToUpperInvariant();

            puntos.Add(new PuntoSerie(
                etiqueta,
                valores.TryGetValue(periodo, out var valor) ? valor : 0,
                periodo));
        }

        return puntos;
    }

    private static IReadOnlyList<AlertaDto> ConstruirAlertas(
        TotalesInventario inventario, bool cajaAbierta, int ventasDelMes)
    {
        var alertas = new List<AlertaDto>();

        if (!cajaAbierta)
        {
            alertas.Add(new AlertaDto
            {
                Nivel = NivelAlerta.Advertencia,
                Titulo = "La caja está cerrada",
                Detalle = "Ábrala para poder registrar ventas en el punto de venta.",
                ModuloDestino = Domain.Constants.Modulos.Caja
            });
        }

        if (inventario.Agotados > 0)
        {
            alertas.Add(new AlertaDto
            {
                Nivel = NivelAlerta.Critica,
                Titulo = inventario.Agotados == 1
                    ? "1 producto agotado"
                    : $"{inventario.Agotados} productos agotados",
                Detalle = "No hay existencias disponibles para la venta.",
                ModuloDestino = Domain.Constants.Modulos.Inventario
            });
        }

        if (inventario.BajoMinimo > 0)
        {
            alertas.Add(new AlertaDto
            {
                Nivel = NivelAlerta.Advertencia,
                Titulo = inventario.BajoMinimo == 1
                    ? "1 producto bajo el mínimo"
                    : $"{inventario.BajoMinimo} productos bajo el mínimo",
                Detalle = "Conviene programar una compra para reponer existencias.",
                ModuloDestino = Domain.Constants.Modulos.Compras
            });
        }

        if (inventario.TotalProductos == 0)
        {
            alertas.Add(new AlertaDto
            {
                Nivel = NivelAlerta.Informacion,
                Titulo = "Aún no hay productos registrados",
                Detalle = "Cree su primer producto para empezar a operar.",
                ModuloDestino = Domain.Constants.Modulos.Productos
            });
        }
        else if (ventasDelMes == 0)
        {
            alertas.Add(new AlertaDto
            {
                Nivel = NivelAlerta.Informacion,
                Titulo = "Sin ventas este mes",
                Detalle = "Todavía no se ha registrado ninguna factura en el periodo actual.",
                ModuloDestino = Domain.Constants.Modulos.Ventas
            });
        }

        return alertas;
    }
}
