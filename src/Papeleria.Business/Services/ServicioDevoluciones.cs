using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;
using Papeleria.Business.Security;
using Papeleria.Data.Repositories;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Entities;
using Papeleria.Domain.Enums;
using Papeleria.Domain.Exceptions;

namespace Papeleria.Business.Services;

/// <inheritdoc cref="IServicioDevoluciones" />
public class ServicioDevoluciones : IServicioDevoluciones
{
    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IServicioKardex _kardex;
    private readonly IServicioConfiguracion _configuracion;
    private readonly IContextoSesion _sesion;
    private readonly ILogger<ServicioDevoluciones> _log;

    public ServicioDevoluciones(
        IUnidadDeTrabajoFactory fabrica,
        IServicioKardex kardex,
        IServicioConfiguracion configuracion,
        IContextoSesion sesion,
        ILogger<ServicioDevoluciones> log)
    {
        _fabrica = fabrica;
        _kardex = kardex;
        _configuracion = configuracion;
        _sesion = sesion;
        _log = log;
    }

    // ── Preparación ─────────────────────────────────────────────────────────

    public async Task<VentaDevolvibleDto> PrepararAsync(int ventaId, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Ventas, AccionPermiso.Ver);

        await using var unidad = _fabrica.Crear();

        var venta = await unidad.Contexto.Ventas
                        .AsNoTracking()
                        .Include(v => v.Cliente)
                        .Include(v => v.Detalles)!
                        .ThenInclude(d => d.Producto)!
                        .ThenInclude(p => p!.UnidadMedida)
                        .FirstOrDefaultAsync(v => v.Id == ventaId, ct).ConfigureAwait(false)
                    ?? throw new RegistroNoEncontradoException("la venta", ventaId);

        if (venta.Estado == EstadoVenta.Anulada)
        {
            throw new NegocioException(
                "La factura está anulada: su mercancía ya volvió completa al inventario.");
        }

        var devueltoPorProducto = await ObtenerDevueltoAsync(unidad, ventaId, ct).ConfigureAwait(false);

        var lineas = venta.Detalles
            .Select(d => new LineaDevolvibleDto
            {
                ProductoId = d.ProductoId,
                Codigo = d.Producto?.Codigo ?? string.Empty,
                Descripcion = d.DescripcionProducto,
                UnidadAbreviatura = d.Producto?.UnidadMedida?.Abreviatura ?? string.Empty,
                CantidadVendida = d.Cantidad,
                CantidadDevuelta = devueltoPorProducto.TryGetValue(d.ProductoId, out var ya) ? ya : 0,
                ValorUnitario = CalcularValorNetoUnitario(d),
                CostoUnitario = d.CostoUnitario,
                ReponeInventario = d.Producto?.ControlaExistencias ?? true
            })
            .ToList();

        var totalDevuelto = await unidad.Contexto.Devoluciones
            .AsNoTracking()
            .Where(d => d.VentaId == ventaId)
            .SumAsync(d => (double?)d.Total, ct).ConfigureAwait(false) ?? 0;

        return new VentaDevolvibleDto
        {
            VentaId = venta.Id,
            NumeroFactura = venta.NumeroFactura,
            Fecha = venta.Fecha,
            ClienteNombre = venta.Cliente?.Nombre ?? string.Empty,
            Total = venta.Total,
            TotalDevuelto = Dinero.Redondear(totalDevuelto),
            Lineas = lineas
        };
    }

    /// <summary>
    /// Precio neto de cada unidad: lo que el cliente pagó por ella una vez aplicado
    /// el descuento de su renglón. Es el importe que hay que reintegrarle.
    /// </summary>
    private static decimal CalcularValorNetoUnitario(VentaDetalle detalle) =>
        detalle.Cantidad == 0
            ? Dinero.Redondear(detalle.PrecioUnitario)
            : Dinero.DividirSeguro(detalle.Subtotal - detalle.ValorDescuento, detalle.Cantidad);

    private static async Task<Dictionary<int, decimal>> ObtenerDevueltoAsync(
        IUnidadDeTrabajo unidad, int ventaId, CancellationToken ct)
    {
        var devueltos = await unidad.Contexto.DevolucionDetalles
            .AsNoTracking()
            .Where(d => d.Devolucion!.VentaId == ventaId)
            .GroupBy(d => d.ProductoId)
            .Select(g => new { ProductoId = g.Key, Cantidad = g.Sum(x => (double)x.Cantidad) })
            .ToListAsync(ct).ConfigureAwait(false);

        return devueltos.ToDictionary(d => d.ProductoId, d => Dinero.Redondear(d.Cantidad));
    }

    // ── Registro ────────────────────────────────────────────────────────────

    public async Task<DevolucionDto> RegistrarAsync(
        SolicitudDevolucion solicitud, CancellationToken ct = default)
    {
        // Devolver es cosa del mostrador: lo hace quien atiende, no solo el dueño.
        _sesion.Exigir(Modulos.Ventas, AccionPermiso.Editar);

        if (string.IsNullOrWhiteSpace(solicitud.Motivo))
        {
            throw new NegocioException("Indique el motivo de la devolución.");
        }

        var pedidas = solicitud.Lineas.Where(l => l.Cantidad > 0).ToList();

        if (pedidas.Count == 0)
        {
            throw new NegocioException("Indique al menos un producto a devolver.");
        }

        var usuarioId = _sesion.UsuarioIdRequerido;

        await using var unidad = _fabrica.Crear();

        var preparada = await PrepararInternoAsync(unidad, solicitud.VentaId, ct).ConfigureAwait(false);

        // Nadie puede devolver más de lo que compró, ni dos veces lo mismo.
        foreach (var pedida in pedidas)
        {
            var linea = preparada.Lineas.FirstOrDefault(l => l.ProductoId == pedida.ProductoId)
                        ?? throw new NegocioException(
                            "Uno de los productos no pertenece a esta factura.");

            if (pedida.Cantidad > linea.Disponible)
            {
                throw new NegocioException(
                    $"De «{linea.Descripcion}» solo quedan {linea.Disponible:N2} por devolver.");
            }
        }

        var devolucionId = await unidad.EjecutarEnTransaccionAsync(async token =>
        {
            var venta = await unidad.Contexto.Ventas
                            .FirstOrDefaultAsync(v => v.Id == solicitud.VentaId, token).ConfigureAwait(false)
                        ?? throw new RegistroNoEncontradoException("la venta", solicitud.VentaId);

            var sesionCaja = await unidad.Contexto.CajaSesiones
                .FirstOrDefaultAsync(s => s.Estado == EstadoCajaSesion.Abierta, token)
                .ConfigureAwait(false);

            var numero = await _configuracion.ReservarConsecutivoAsync(
                unidad, ClavesConfiguracion.DevolucionPrefijo,
                ClavesConfiguracion.DevolucionConsecutivo, token).ConfigureAwait(false);

            var devolucion = new Devolucion
            {
                Numero = numero,
                VentaId = venta.Id,
                Fecha = DateTime.Now,
                UsuarioId = usuarioId,
                CajaSesionId = sesionCaja?.Id,
                Motivo = solicitud.Motivo.Trim()
            };

            unidad.Contexto.Devoluciones.Add(devolucion);
            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            decimal total = 0;
            decimal costo = 0;

            foreach (var pedida in pedidas)
            {
                var linea = preparada.Lineas.First(l => l.ProductoId == pedida.ProductoId);

                var importe = Dinero.Redondear(pedida.Cantidad * linea.ValorUnitario);
                var costoLinea = Dinero.Redondear(pedida.Cantidad * linea.CostoUnitario);

                total += importe;
                costo += costoLinea;

                unidad.Contexto.DevolucionDetalles.Add(new DevolucionDetalle
                {
                    DevolucionId = devolucion.Id,
                    ProductoId = linea.ProductoId,
                    DescripcionProducto = linea.Descripcion,
                    Cantidad = pedida.Cantidad,
                    ValorUnitario = linea.ValorUnitario,
                    CostoUnitario = linea.CostoUnitario,
                    Total = importe
                });

                var producto = await unidad.Contexto.Productos
                                   .FirstOrDefaultAsync(p => p.Id == linea.ProductoId, token)
                                   .ConfigureAwait(false)
                               ?? throw new NegocioException(
                                   "Uno de los productos devueltos ya no existe.");

                // La mercancía vuelve al estante; una fotocopia devuelta no.
                await _kardex.RegistrarAsync(
                    unidad, producto, TipoMovimientoKardex.DevolucionVenta,
                    pedida.Cantidad, linea.CostoUnitario,
                    $"Devolución de la venta {venta.NumeroFactura}",
                    devolucion.Numero, usuarioId, token).ConfigureAwait(false);
            }

            devolucion.Total = Dinero.Redondear(total);
            devolucion.CostoTotal = Dinero.Redondear(costo);

            // El dinero sale del cajón solo si la venta había entrado en efectivo.
            if (sesionCaja is not null)
            {
                unidad.Contexto.MovimientosCaja.Add(new MovimientoCaja
                {
                    CajaSesionId = sesionCaja.Id,
                    Fecha = devolucion.Fecha,
                    Tipo = TipoMovimientoCaja.Devolucion,
                    Monto = devolucion.Total,
                    Concepto = $"Devolución {devolucion.Numero} de la venta {venta.NumeroFactura}",
                    UsuarioId = usuarioId,
                    VentaId = venta.Id,
                    AfectaEfectivo = venta.MetodoPago is MetodoPago.Efectivo or MetodoPago.Mixto
                });
            }

            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            return devolucion.Id;
        }, ct).ConfigureAwait(false);

        _log.LogInformation(
            "Devolución registrada sobre la venta {Venta} por {Usuario}",
            solicitud.VentaId, _sesion.Usuario?.NombreUsuario);

        return await ObtenerAsync(devolucionId, ct).ConfigureAwait(false);
    }

    private async Task<VentaDevolvibleDto> PrepararInternoAsync(
        IUnidadDeTrabajo unidad, int ventaId, CancellationToken ct)
    {
        var venta = await unidad.Contexto.Ventas
                        .AsNoTracking()
                        .Include(v => v.Detalles)!
                        .ThenInclude(d => d.Producto)
                        .FirstOrDefaultAsync(v => v.Id == ventaId, ct).ConfigureAwait(false)
                    ?? throw new RegistroNoEncontradoException("la venta", ventaId);

        if (venta.Estado == EstadoVenta.Anulada)
        {
            throw new NegocioException("La factura está anulada y no admite devoluciones.");
        }

        var devuelto = await ObtenerDevueltoAsync(unidad, ventaId, ct).ConfigureAwait(false);

        return new VentaDevolvibleDto
        {
            VentaId = venta.Id,
            NumeroFactura = venta.NumeroFactura,
            Lineas = venta.Detalles.Select(d => new LineaDevolvibleDto
            {
                ProductoId = d.ProductoId,
                Descripcion = d.DescripcionProducto,
                CantidadVendida = d.Cantidad,
                CantidadDevuelta = devuelto.TryGetValue(d.ProductoId, out var ya) ? ya : 0,
                ValorUnitario = CalcularValorNetoUnitario(d),
                CostoUnitario = d.CostoUnitario,
                ReponeInventario = d.Producto?.ControlaExistencias ?? true
            }).ToList()
        };
    }

    // ── Consulta ────────────────────────────────────────────────────────────

    public async Task<List<DevolucionDto>> ListarPorVentaAsync(int ventaId, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        return await Proyectar(unidad)
            .Where(d => d.VentaId == ventaId)
            .OrderByDescending(d => d.Fecha)
            .Select(ProyeccionDto)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<DevolucionDto>> ListarAsync(
        DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Ventas, AccionPermiso.Ver);

        var limite = hasta.Date.AddDays(1);

        await using var unidad = _fabrica.Crear();

        return await Proyectar(unidad)
            .Where(d => d.Fecha >= desde.Date && d.Fecha < limite)
            .OrderByDescending(d => d.Fecha)
            .Select(ProyeccionDto)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    private async Task<DevolucionDto> ObtenerAsync(int devolucionId, CancellationToken ct)
    {
        await using var unidad = _fabrica.Crear();

        return await Proyectar(unidad)
                   .Where(d => d.Id == devolucionId)
                   .Select(ProyeccionDto)
                   .FirstOrDefaultAsync(ct).ConfigureAwait(false)
               ?? throw new RegistroNoEncontradoException("la devolución", devolucionId);
    }

    private static IQueryable<Devolucion> Proyectar(IUnidadDeTrabajo unidad) =>
        unidad.Contexto.Devoluciones.AsNoTracking();

    private static System.Linq.Expressions.Expression<Func<Devolucion, DevolucionDto>> ProyeccionDto =>
        d => new DevolucionDto
        {
            Id = d.Id,
            Numero = d.Numero,
            Fecha = d.Fecha,
            NumeroFactura = d.Venta!.NumeroFactura,
            ClienteNombre = d.Venta.Cliente!.Nombre,
            UsuarioNombre = d.Usuario!.NombreCompleto,
            Motivo = d.Motivo,
            Total = d.Total,
            CantidadLineas = d.Detalles.Count
        };
}
