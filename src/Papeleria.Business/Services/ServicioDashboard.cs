using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;
using Papeleria.Business.Security;
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

    /// <summary>Días sin venderse a partir de los cuales un artículo se considera parado.</summary>
    private const int DiasSinRotacion = 90;

    /// <summary>Horas de turno abierto a partir de las cuales se avisa del olvido.</summary>
    private const int HorasParaAvisarCaja = 12;

    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IServicioVentas _ventas;
    private readonly IServicioKardex _kardex;
    private readonly IServicioCartera _cartera;
    private readonly IServicioCaja _caja;
    private readonly IServicioBackup _respaldo;
    private readonly IContextoSesion _sesion;

    public ServicioDashboard(
        IUnidadDeTrabajoFactory fabrica,
        IServicioVentas ventas,
        IServicioKardex kardex,
        IServicioCartera cartera,
        IServicioCaja caja,
        IServicioBackup respaldo,
        IContextoSesion sesion)
    {
        _fabrica = fabrica;
        _ventas = ventas;
        _kardex = kardex;
        _cartera = cartera;
        _caja = caja;
        _respaldo = respaldo;
        _sesion = sesion;
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
        // ── Estado del turno de caja ────────────────────────────────────────
        var turno = await unidad.Contexto.CajaSesiones
            .AsNoTracking()
            .Where(s => s.Estado == EstadoCajaSesion.Abierta)
            .Select(s => new { s.Id, s.FechaApertura, Usuario = s.UsuarioApertura!.NombreCompleto })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        var cajaAbierta = turno is not null;

        // Las cifras de caja y de cartera se piden a sus servicios, que ya las
        // calculan bien, pero solo si el usuario puede verlas: al de bodega no le
        // corresponde el dinero del cajón ni la deuda de los clientes.
        var puedeVerCaja = _sesion.Puede(Domain.Constants.Modulos.Caja);
        var puedeVerCartera = _sesion.Puede(Domain.Constants.Modulos.Cartera);

        decimal efectivoEnCaja = 0;

        if (cajaAbierta && puedeVerCaja)
        {
            var arqueo = await _caja.CalcularArqueoAsync(turno!.Id, ct).ConfigureAwait(false);
            efectivoEnCaja = arqueo.MontoEsperado;
        }

        var cartera = puedeVerCartera
            ? await _cartera.ObtenerResumenAsync(new FiltroCartera(), ct).ConfigureAwait(false)
            : new ResumenCarteraDto();

        // ── Comparación honesta: contra el mismo mes del año pasado ─────────
        var inicioMesAnioAnterior = inicioMes.AddYears(-1);

        var ventasAnioAnterior = await ObtenerTotalesVentaAsync(
            unidad, inicioMesAnioAnterior, inicioMesAnioAnterior.AddMonths(1), ct).ConfigureAwait(false);

        var hayHistorialAnual = await unidad.Contexto.Ventas
            .AnyAsync(v => v.Fecha < inicioMes.AddMonths(-11) && v.Estado == EstadoVenta.Completada, ct)
            .ConfigureAwait(false);

        // Y el día de hoy contra el mismo día de la semana pasada, no contra ayer.
        var mismoDiaSemanaAnterior = hoy.AddDays(-7);

        var ventasSemanaAnterior = await ObtenerTotalesVentaAsync(
            unidad, mismoDiaSemanaAnterior, mismoDiaSemanaAnterior.AddDays(1), ct).ConfigureAwait(false);

        var parados = await ObtenerSinRotacionAsync(unidad, hoy.AddDays(-DiasSinRotacion), ct)
            .ConfigureAwait(false);

        var bajoCosto = await ContarBajoCostoAsync(unidad, ct).ConfigureAwait(false);

        // El estado del respaldo se lee de la configuración, no de la base de negocio.
        var respaldo = _respaldo.ObtenerEstado();

        var alertas = ConstruirAlertas(
            inventario, cajaAbierta, ventasMes.Cantidad, cartera, turno?.FechaApertura,
            bajoCosto, puedeVerCartera, respaldo);

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
            CajaAbiertaDesde = turno?.FechaApertura,
            CajaAbiertaPor = turno?.Usuario,
            EfectivoEnCaja = efectivoEnCaja,
            PuedeVerCaja = puedeVerCaja,

            SaldoCartera = cartera.SaldoTotal,
            CarteraVencida = cartera.VencidoMas60,
            ClientesConDeuda = cartera.ClientesConDeuda,
            PuedeVerCartera = puedeVerCartera,

            VentasMismoMesAnioAnterior = ventasAnioAnterior.Total,
            VariacionInteranual = Dinero.VariacionPorcentual(ventasMes.Total, ventasAnioAnterior.Total),
            HayHistorialAnual = hayHistorialAnual,
            MontoMismoDiaSemanaAnterior = ventasSemanaAnterior.Total,
            VariacionDiaria = Dinero.VariacionPorcentual(ventasHoy.Total, ventasSemanaAnterior.Total),

            ProductosSinRotacion = parados.Cantidad,
            ValorSinRotacion = parados.Valor,
            ProductosBajoCosto = bajoCosto,

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

    private sealed record ExistenciasParadas(int Cantidad, decimal Valor);

    /// <summary>
    /// Mercancía con existencias que no se ha vendido en el periodo indicado. Es
    /// dinero quieto en la estantería: lo que conviene liquidar o devolver.
    /// </summary>
    private static async Task<ExistenciasParadas> ObtenerSinRotacionAsync(
        IUnidadDeTrabajo unidad, DateTime desde, CancellationToken ct)
    {
        var vendidosRecientemente = unidad.Contexto.VentaDetalles
            .Where(d => d.Venta!.Fecha >= desde && d.Venta.Estado == EstadoVenta.Completada)
            .Select(d => d.ProductoId);

        var datos = await unidad.Contexto.Productos
            .AsNoTracking()
            .Where(p => p.Activo
                        && p.Tipo == TipoProducto.Producto
                        && p.StockActual > 0
                        && !vendidosRecientemente.Contains(p.Id))
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Cantidad = g.Count(),
                Valor = g.Sum(p => (double)(p.StockActual * p.Costo))
            })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return datos is null
            ? new ExistenciasParadas(0, 0)
            : new ExistenciasParadas(datos.Cantidad, Dinero.Redondear(datos.Valor));
    }

    /// <summary>
    /// Artículos cuyo precio quedó por debajo del costo. Pasa solo: el costo promedio
    /// sube con cada compra y, si el precio no se actualiza, se vende a pérdida sin
    /// que nadie se entere hasta cuadrar el mes.
    /// </summary>
    private static Task<int> ContarBajoCostoAsync(IUnidadDeTrabajo unidad, CancellationToken ct) =>
        unidad.Contexto.Productos
            .AsNoTracking()
            .CountAsync(p => p.Activo
                             && p.Tipo == TipoProducto.Producto
                             && p.Costo > 0
                             && p.PrecioVenta < p.Costo, ct);

    /// <summary>
    /// Avisos del día a día. Las de puesta en marcha solo aparecen mientras el
    /// negocio está a medio montar; después ceden el sitio a las que de verdad
    /// exigen actuar hoy.
    /// </summary>
    private static IReadOnlyList<AlertaDto> ConstruirAlertas(
        TotalesInventario inventario,
        bool cajaAbierta,
        int ventasDelMes,
        ResumenCarteraDto cartera,
        DateTime? cajaDesde,
        int productosBajoCosto,
        bool puedeVerCartera,
        EstadoRespaldoDto respaldo)
    {
        var alertas = new List<AlertaDto>();

        // Lo primero de todo. Perder una venta se aguanta; perder la base de datos, no.
        // La copia automática falla en silencio si el destino no está disponible, así
        // que este aviso es la única forma de enterarse antes de que sea tarde.
        if (respaldo.Atrasado)
        {
            alertas.Add(new AlertaDto
            {
                Nivel = NivelAlerta.Critica,
                Titulo = respaldo.NuncaSeHaHecho
                    ? "Nunca se ha respaldado la información"
                    : $"La última copia de seguridad es de hace {respaldo.DiasDesdeLaUltima} días",
                Detalle = respaldo.Automatico
                    ? $"La copia automática no está llegando a «{respaldo.Carpeta}». " +
                      "Puede que la memoria esté desconectada."
                    : "Las copias automáticas están apagadas. Si se daña el disco, no hay de dónde volver.",
                ModuloDestino = Domain.Constants.Modulos.Configuracion
            });
        }

        // Vender por debajo del costo es la pérdida más silenciosa que hay.
        if (productosBajoCosto > 0)
        {
            alertas.Add(new AlertaDto
            {
                Nivel = NivelAlerta.Critica,
                Titulo = productosBajoCosto == 1
                    ? "1 producto se vende por debajo del costo"
                    : $"{productosBajoCosto} productos se venden por debajo del costo",
                Detalle = "Subió el costo y el precio no se actualizó: cada venta pierde dinero.",
                ModuloDestino = Domain.Constants.Modulos.Productos
            });
        }

        if (puedeVerCartera && cartera.VencidoMas60 > 0)
        {
            alertas.Add(new AlertaDto
            {
                Nivel = NivelAlerta.Critica,
                Titulo = $"{Formatos.Moneda(cartera.VencidoMas60)} con más de 60 días",
                Detalle = "Es la deuda que se vuelve incobrable. Conviene llamar hoy.",
                ModuloDestino = Domain.Constants.Modulos.Cartera
            });
        }

        // Un turno que lleva medio día abierto casi siempre es un cierre olvidado.
        if (cajaAbierta && cajaDesde is { } desde &&
            (DateTime.Now - desde).TotalHours > HorasParaAvisarCaja)
        {
            alertas.Add(new AlertaDto
            {
                Nivel = NivelAlerta.Advertencia,
                Titulo = "La caja lleva más de 12 horas abierta",
                Detalle = $"Se abrió el {desde:dd/MM} a las {desde:HH:mm}. ¿Quedó sin cerrar?",
                ModuloDestino = Domain.Constants.Modulos.Caja
            });
        }

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

        // Solo mientras el negocio está a medio montar.
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
