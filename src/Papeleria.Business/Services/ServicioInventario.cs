using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;
using Papeleria.Business.Security;
using Papeleria.Data.Repositories;
using Papeleria.Domain.Common;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Entities;
using Papeleria.Domain.Enums;
using Papeleria.Domain.Exceptions;

namespace Papeleria.Business.Services;

/// <inheritdoc cref="IServicioInventario" />
public class ServicioInventario : IServicioInventario
{
    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IServicioProductos _productos;
    private readonly IServicioKardex _kardex;
    private readonly IContextoSesion _sesion;
    private readonly ILogger<ServicioInventario> _log;

    public ServicioInventario(
        IUnidadDeTrabajoFactory fabrica,
        IServicioProductos productos,
        IServicioKardex kardex,
        IContextoSesion sesion,
        ILogger<ServicioInventario> log)
    {
        _fabrica = fabrica;
        _productos = productos;
        _kardex = kardex;
        _sesion = sesion;
        _log = log;
    }

    public Task<ResultadoPaginado<ProductoListadoDto>> ConsultarExistenciasAsync(
        FiltroProductos filtro, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Inventario, AccionPermiso.Ver);
        return _productos.BuscarAsync(filtro, ct);
    }

    public async Task<ResumenInventarioDto> ObtenerResumenAsync(CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        var consulta = unidad.Contexto.Productos.AsNoTracking();

        var totales = await consulta
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Activos = g.Count(p => p.Activo),
                Unidades = g.Sum(p => (double)p.StockActual),
                ValorCosto = g.Sum(p => (double)(p.StockActual * p.Costo)),
                ValorVenta = g.Sum(p => (double)(p.StockActual * p.PrecioVenta)),
                Agotados = g.Count(p => p.Activo && p.StockActual <= 0),
                BajoMinimo = g.Count(p => p.Activo && p.StockActual > 0 && p.StockActual <= p.StockMinimo),
                SobreMaximo = g.Count(p => p.Activo && p.StockMaximo > 0 && p.StockActual > p.StockMaximo)
            })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        if (totales is null)
        {
            return new ResumenInventarioDto();
        }

        return new ResumenInventarioDto
        {
            TotalProductos = totales.Total,
            ProductosActivos = totales.Activos,
            UnidadesTotales = Dinero.Redondear(totales.Unidades),
            ValorCosto = Dinero.Redondear(totales.ValorCosto),
            ValorVenta = Dinero.Redondear(totales.ValorVenta),
            Agotados = totales.Agotados,
            BajoMinimo = totales.BajoMinimo,
            SobreMaximo = totales.SobreMaximo
        };
    }

    public Task RegistrarEntradaAsync(SolicitudMovimientoInventario solicitud, CancellationToken ct = default) =>
        RegistrarMovimientoAsync(solicitud, TipoMovimientoKardex.EntradaManual, ct);

    public Task RegistrarSalidaAsync(SolicitudMovimientoInventario solicitud, CancellationToken ct = default) =>
        RegistrarMovimientoAsync(solicitud, TipoMovimientoKardex.SalidaManual, ct);

    private async Task RegistrarMovimientoAsync(
        SolicitudMovimientoInventario solicitud, TipoMovimientoKardex tipo, CancellationToken ct)
    {
        _sesion.Exigir(Modulos.Inventario, AccionPermiso.Editar);

        if (solicitud.Cantidad <= 0)
        {
            throw new NegocioException("La cantidad debe ser mayor que cero.");
        }

        if (string.IsNullOrWhiteSpace(solicitud.Motivo))
        {
            throw new NegocioException("Escriba el motivo del movimiento.");
        }

        var usuarioId = _sesion.UsuarioIdRequerido;

        await using var unidad = _fabrica.Crear();

        await unidad.EjecutarEnTransaccionAsync(async token =>
        {
            var producto = await unidad.Contexto.Productos
                               .FirstOrDefaultAsync(p => p.Id == solicitud.ProductoId, token).ConfigureAwait(false)
                           ?? throw new RegistroNoEncontradoException("el producto", solicitud.ProductoId);

            var costo = solicitud.CostoUnitario ?? producto.Costo;

            await _kardex.RegistrarAsync(
                unidad, producto, tipo, solicitud.Cantidad, costo,
                solicitud.Motivo, solicitud.DocumentoReferencia, usuarioId, token)
                .ConfigureAwait(false);

            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            _log.LogInformation("{Tipo} de {Cantidad} unidades de {Producto}",
                tipo, solicitud.Cantidad, producto.Nombre);

            return true;
        }, ct).ConfigureAwait(false);
    }

    public async Task RegistrarAjusteAsync(
        int productoId, decimal stockReal, string motivo, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Inventario, AccionPermiso.Editar);

        if (stockReal < 0)
        {
            throw new NegocioException("El stock real no puede ser negativo.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new NegocioException("Escriba el motivo del ajuste.");
        }

        var usuarioId = _sesion.UsuarioIdRequerido;

        await using var unidad = _fabrica.Crear();

        await unidad.EjecutarEnTransaccionAsync(async token =>
        {
            var producto = await unidad.Contexto.Productos
                               .FirstOrDefaultAsync(p => p.Id == productoId, token).ConfigureAwait(false)
                           ?? throw new RegistroNoEncontradoException("el producto", productoId);

            var diferencia = stockReal - producto.StockActual;

            if (diferencia == 0)
            {
                throw new NegocioException(
                    "El stock real coincide con el registrado: no hay nada que ajustar.");
            }

            var tipo = diferencia > 0
                ? TipoMovimientoKardex.AjustePositivo
                : TipoMovimientoKardex.AjusteNegativo;

            await _kardex.RegistrarAsync(
                unidad, producto, tipo, Math.Abs(diferencia), producto.Costo,
                $"Ajuste de inventario: {motivo}", null, usuarioId, token)
                .ConfigureAwait(false);

            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            _log.LogInformation("Ajuste de {Producto}: {Diferencia:+0.##;-0.##} unidades ({Motivo})",
                producto.Nombre, diferencia, motivo);

            return true;
        }, ct).ConfigureAwait(false);
    }

    public async Task RegistrarTransferenciaAsync(
        SolicitudTransferencia solicitud, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Inventario, AccionPermiso.Editar);

        if (solicitud.Cantidad <= 0)
        {
            throw new NegocioException("La cantidad a transferir debe ser mayor que cero.");
        }

        if (string.IsNullOrWhiteSpace(solicitud.UbicacionDestino))
        {
            throw new NegocioException("Indique la ubicación de destino.");
        }

        var usuarioId = _sesion.UsuarioIdRequerido;

        await using var unidad = _fabrica.Crear();

        await unidad.EjecutarEnTransaccionAsync(async token =>
        {
            var producto = await unidad.Contexto.Productos
                               .FirstOrDefaultAsync(p => p.Id == solicitud.ProductoId, token).ConfigureAwait(false)
                           ?? throw new RegistroNoEncontradoException("el producto", solicitud.ProductoId);

            if (producto.StockActual < solicitud.Cantidad)
            {
                throw new NegocioException(
                    $"No hay existencias suficientes de «{producto.Nombre}» para transferir " +
                    $"{solicitud.Cantidad:N2} unidades (disponible: {producto.StockActual:N2}).");
            }

            var origen = string.IsNullOrWhiteSpace(solicitud.UbicacionOrigen)
                ? producto.Ubicacion ?? "sin ubicación"
                : solicitud.UbicacionOrigen.Trim();

            var destino = solicitud.UbicacionDestino.Trim();

            var motivo = $"Traslado de «{origen}» a «{destino}»" +
                         (string.IsNullOrWhiteSpace(solicitud.Observaciones)
                             ? string.Empty
                             : $". {solicitud.Observaciones.Trim()}");

            // La transferencia reubica mercancía: el kardex la documenta sin alterar el total.
            await _kardex.RegistrarAsync(
                unidad, producto, TipoMovimientoKardex.Transferencia,
                solicitud.Cantidad, producto.Costo, motivo, null, usuarioId, token)
                .ConfigureAwait(false);

            producto.Ubicacion = destino;

            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            _log.LogInformation("Transferencia de {Cantidad} de {Producto}: {Origen} → {Destino}",
                solicitud.Cantidad, producto.Nombre, origen, destino);

            return true;
        }, ct).ConfigureAwait(false);
    }
}
