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

/// <inheritdoc cref="IServicioVentas" />
public class ServicioVentas : IServicioVentas
{
    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IServicioKardex _kardex;
    private readonly IServicioCaja _caja;
    private readonly IServicioConfiguracion _configuracion;
    private readonly IContextoSesion _sesion;
    private readonly ILogger<ServicioVentas> _log;

    public ServicioVentas(
        IUnidadDeTrabajoFactory fabrica,
        IServicioKardex kardex,
        IServicioCaja caja,
        IServicioConfiguracion configuracion,
        IContextoSesion sesion,
        ILogger<ServicioVentas> log)
    {
        _fabrica = fabrica;
        _kardex = kardex;
        _caja = caja;
        _configuracion = configuracion;
        _sesion = sesion;
        _log = log;
    }

    /// <summary>
    /// Consultar facturas se autoriza por el punto de venta o por el historial: el
    /// administrador puede retirar uno de los dos permisos sin cerrar el otro módulo.
    /// </summary>
    /// <summary>
    /// Impide fiar por encima del cupo. Sin este control la deuda crece sin tope y el
    /// negocio se entera cuando ya no hay cómo cobrar.
    /// </summary>
    private static async Task ComprobarCupoAsync(
        IUnidadDeTrabajo unidad, Cliente cliente, decimal total, CancellationToken ct)
    {
        if (cliente.EsProtegido)
        {
            throw new NegocioException(
                "No se puede fiar al consumidor final. Registre al cliente para venderle a crédito.");
        }

        if (cliente.LimiteCredito <= 0)
        {
            throw new NegocioException(
                $"{cliente.Nombre} no tiene cupo de crédito asignado. " +
                "Edite su ficha para autorizarle un cupo.");
        }

        var fiado = await unidad.Contexto.Ventas
            .AsNoTracking()
            .Where(v => v.ClienteId == cliente.Id
                        && v.MetodoPago == MetodoPago.Credito
                        && v.Estado == EstadoVenta.Completada)
            .SumAsync(v => (double?)v.Total, ct).ConfigureAwait(false) ?? 0;

        var abonado = await unidad.Contexto.AbonosCliente
            .AsNoTracking()
            .Where(a => a.ClienteId == cliente.Id && !a.Anulado)
            .SumAsync(a => (double?)a.Monto, ct).ConfigureAwait(false) ?? 0;

        var saldo = Dinero.Redondear(fiado - abonado);
        var disponible = cliente.LimiteCredito - saldo;

        if (total > disponible)
        {
            throw new NegocioException(
                $"{cliente.Nombre} debe {Formatos.Moneda(saldo)} y su cupo es " +
                $"{Formatos.Moneda(cliente.LimiteCredito)}. " +
                $"Sólo se le puede fiar hasta {Formatos.Moneda(Math.Max(disponible, 0))}.");
        }
    }

    private void ExigirLecturaDeVentas()
    {
        if (_sesion.Puede(Modulos.HistorialVentas))
        {
            return;
        }

        _sesion.Exigir(Modulos.Ventas, AccionPermiso.Ver);
    }

    public async Task<ResultadoPaginado<VentaResumenDto>> BuscarAsync(
        FiltroVentas filtro, CancellationToken ct = default)
    {
        ExigirLecturaDeVentas();

        await using var unidad = _fabrica.Crear();

        var consulta = ConstruirConsulta(unidad, filtro);

        var total = await consulta.CountAsync(ct).ConfigureAwait(false);

        var elementos = await consulta
            .OrderByDescending(v => v.Fecha)
            .ThenByDescending(v => v.Id)
            .Skip((Math.Max(filtro.Pagina, 1) - 1) * filtro.TamanoPagina)
            .Take(filtro.TamanoPagina)
            .Select(ProyeccionResumen)
            .ToListAsync(ct).ConfigureAwait(false);

        return new ResultadoPaginado<VentaResumenDto>(elementos, total, filtro.Pagina, filtro.TamanoPagina);
    }

    /// <summary>
    /// Traduce el filtro a condiciones SQL. Se comparte entre el listado y el resumen
    /// para que las cifras del encabezado correspondan exactamente a lo que se lista.
    /// </summary>
    private static IQueryable<Venta> ConstruirConsulta(IUnidadDeTrabajo unidad, FiltroVentas filtro)
    {
        var consulta = unidad.Contexto.Ventas.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var termino = filtro.Texto.Trim();
            consulta = consulta.Where(v =>
                EF.Functions.Like(v.NumeroFactura, $"%{termino}%") ||
                EF.Functions.Like(v.Cliente!.Nombre, $"%{termino}%") ||
                (v.Cliente!.NumeroDocumento != null &&
                 EF.Functions.Like(v.Cliente!.NumeroDocumento, $"%{termino}%")));
        }

        if (filtro.ClienteId is > 0)
        {
            consulta = consulta.Where(v => v.ClienteId == filtro.ClienteId);
        }

        if (filtro.UsuarioId is > 0)
        {
            consulta = consulta.Where(v => v.UsuarioId == filtro.UsuarioId);
        }

        if (filtro.Desde is { } desde)
        {
            var inicio = desde.Date;
            consulta = consulta.Where(v => v.Fecha >= inicio);
        }

        if (filtro.Hasta is { } hasta)
        {
            // El filtro es inclusivo: se toma hasta el último instante del día indicado.
            var fin = hasta.Date.AddDays(1);
            consulta = consulta.Where(v => v.Fecha < fin);
        }

        if (filtro.MetodoPago is { } metodo)
        {
            consulta = consulta.Where(v => v.MetodoPago == metodo);
        }

        if (!filtro.IncluirAnuladas)
        {
            consulta = consulta.Where(v => v.Estado == EstadoVenta.Completada);
        }

        return consulta;
    }

    private static System.Linq.Expressions.Expression<Func<Venta, VentaResumenDto>> ProyeccionResumen =>
        v => new VentaResumenDto
        {
            Id = v.Id,
            NumeroFactura = v.NumeroFactura,
            Fecha = v.Fecha,
            ClienteId = v.ClienteId,
            ClienteNombre = v.Cliente!.Nombre,
            UsuarioNombre = v.Usuario!.NombreCompleto,
            Subtotal = v.Subtotal,
            TotalDescuento = v.TotalDescuento,
            TotalIva = v.TotalIva,
            Total = v.Total,
            CostoTotal = v.CostoTotal,
            CantidadItems = v.Detalles.Count,
            MetodoPago = v.MetodoPago,
            Estado = v.Estado
        };

    public async Task<ResumenVentasDto> ObtenerResumenAsync(
        FiltroVentas filtro, CancellationToken ct = default)
    {
        ExigirLecturaDeVentas();

        await using var unidad = _fabrica.Crear();

        // Las cifras se calculan sobre todo el rango filtrado, no sobre la página
        // mostrada: un total que solo sumara lo visible daría una lectura falsa.
        var consulta = ConstruirConsulta(unidad, filtro);

        var datos = await consulta
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Facturas = g.Count(v => v.Estado == EstadoVenta.Completada),
                Total = g.Sum(v => v.Estado == EstadoVenta.Completada ? (double)v.Total : 0d),
                Utilidad = g.Sum(v => v.Estado == EstadoVenta.Completada
                    ? (double)(v.Subtotal - v.TotalDescuento - v.CostoTotal)
                    : 0d),
                Anuladas = g.Count(v => v.Estado == EstadoVenta.Anulada),
                TotalAnulado = g.Sum(v => v.Estado == EstadoVenta.Anulada ? (double)v.Total : 0d),
                Lineas = g.Sum(v => v.Estado == EstadoVenta.Completada ? v.Detalles.Count : 0)
            })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        if (datos is null)
        {
            return new ResumenVentasDto();
        }

        return new ResumenVentasDto
        {
            CantidadFacturas = datos.Facturas,
            TotalFacturado = Dinero.Redondear(datos.Total),
            TotalUtilidad = Dinero.Redondear(datos.Utilidad),
            CantidadAnuladas = datos.Anuladas,
            TotalAnulado = Dinero.Redondear(datos.TotalAnulado),
            LineasFacturadas = datos.Lineas
        };
    }

    public async Task<VentaDetalladaDto?> ObtenerDetalleAsync(int ventaId, CancellationToken ct = default)
    {
        ExigirLecturaDeVentas();

        await using var unidad = _fabrica.Crear();
        return await ObtenerDetalleInternoAsync(unidad, ventaId, ct).ConfigureAwait(false);
    }

    private static Task<VentaDetalladaDto?> ObtenerDetalleInternoAsync(
        IUnidadDeTrabajo unidad, int ventaId, CancellationToken ct) =>
        unidad.Contexto.Ventas
            .AsNoTracking()
            .Where(v => v.Id == ventaId)
            .Select(v => new VentaDetalladaDto
            {
                Id = v.Id,
                NumeroFactura = v.NumeroFactura,
                Fecha = v.Fecha,
                ClienteNombre = v.Cliente!.Nombre,
                ClienteDocumento = v.Cliente!.NumeroDocumento,
                ClienteTelefono = v.Cliente!.Telefono,
                ClienteDireccion = v.Cliente!.Direccion,
                UsuarioNombre = v.Usuario!.NombreCompleto,
                Subtotal = v.Subtotal,
                TotalDescuento = v.TotalDescuento,
                TotalIva = v.TotalIva,
                Total = v.Total,
                CostoTotal = v.CostoTotal,
                MetodoPago = v.MetodoPago,
                MontoRecibido = v.MontoRecibido,
                Cambio = v.Cambio,
                Estado = v.Estado,
                FechaAnulacion = v.FechaAnulacion,
                MotivoAnulacion = v.MotivoAnulacion,
                Observaciones = v.Observaciones,
                Lineas = v.Detalles.Select(d => new LineaDocumentoDto
                {
                    ProductoId = d.ProductoId,
                    Codigo = d.Producto!.Codigo,
                    Descripcion = d.DescripcionProducto,
                    UnidadAbreviatura = d.Producto!.UnidadMedida!.Abreviatura,
                    Cantidad = d.Cantidad,
                    ValorUnitario = d.PrecioUnitario,
                    PorcentajeDescuento = d.PorcentajeDescuento,
                    PorcentajeIva = d.PorcentajeIva,
                    ValorDescuento = d.ValorDescuento,
                    ValorIva = d.ValorIva,
                    Subtotal = d.Subtotal,
                    Total = d.Total
                }).ToList()
            })
            .FirstOrDefaultAsync(ct);

    public async Task<VentaDetalladaDto> RegistrarAsync(SolicitudVenta solicitud, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Ventas, AccionPermiso.Crear);
        Validar(solicitud);

        var usuarioId = _sesion.UsuarioIdRequerido;

        await using var unidad = _fabrica.Crear();

        // Sin turno de caja abierto no se puede facturar: el dinero no tendría dónde registrarse.
        var sesionCaja = await unidad.Contexto.CajaSesiones
                             .FirstOrDefaultAsync(s => s.Estado == EstadoCajaSesion.Abierta, ct).ConfigureAwait(false)
                         ?? throw new NegocioException(
                             "No hay una caja abierta. Abra la caja antes de registrar ventas.");

        var cliente = await unidad.Contexto.Clientes
                          .AsNoTracking()
                          .FirstOrDefaultAsync(c => c.Id == solicitud.ClienteId, ct).ConfigureAwait(false)
                      ?? throw new NegocioException("Seleccione un cliente válido.");

        if (solicitud.MetodoPago == MetodoPago.Credito)
        {
            await ComprobarCupoAsync(unidad, cliente, solicitud.Total, ct).ConfigureAwait(false);
        }

        var ventaId = await unidad.EjecutarEnTransaccionAsync(async token =>
        {
            var idsProductos = solicitud.Lineas.Select(l => l.ProductoId).Distinct().ToList();

            var productos = await unidad.Contexto.Productos
                .Where(p => idsProductos.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, token).ConfigureAwait(false);

            // Se valida todo el carrito antes de tocar nada, para que el mensaje de error
            // sea completo y el inventario no quede parcialmente descontado.
            var faltantes = new List<string>();

            foreach (var grupo in solicitud.Lineas.GroupBy(l => l.ProductoId))
            {
                if (!productos.TryGetValue(grupo.Key, out var producto))
                {
                    throw new NegocioException("Uno de los productos del carrito ya no existe.");
                }

                if (!producto.Activo)
                {
                    throw new NegocioException($"El producto «{producto.Nombre}» está inactivo y no puede venderse.");
                }

                var solicitado = grupo.Sum(l => l.Cantidad);

                // Una fotocopia no se puede agotar: no hay existencia que comprobar.
                if (producto.ControlaExistencias && producto.StockActual < solicitado)
                {
                    faltantes.Add(
                        $"• {producto.Nombre}: disponible {producto.StockActual:N2}, solicitado {solicitado:N2}");
                }
            }

            if (faltantes.Count > 0)
            {
                throw new NegocioException(
                    "No hay existencias suficientes para completar la venta:" +
                    Environment.NewLine + string.Join(Environment.NewLine, faltantes));
            }

            var numeroFactura = await _configuracion.ReservarConsecutivoAsync(
                unidad, ClavesConfiguracion.FacturaPrefijo, ClavesConfiguracion.FacturaConsecutivo, token)
                .ConfigureAwait(false);

            var venta = new Venta
            {
                NumeroFactura = numeroFactura,
                Fecha = DateTime.Now,
                ClienteId = cliente.Id,
                UsuarioId = usuarioId,
                CajaSesionId = sesionCaja.Id,
                Subtotal = solicitud.Subtotal,
                TotalDescuento = solicitud.TotalDescuento,
                TotalIva = solicitud.TotalIva,
                Total = solicitud.Total,
                CostoTotal = solicitud.CostoTotal,
                MetodoPago = solicitud.MetodoPago,
                MontoRecibido = Dinero.Redondear(solicitud.MontoRecibido),
                Cambio = solicitud.Cambio,
                Estado = EstadoVenta.Completada,
                Observaciones = Texto.NormalizarOpcional(solicitud.Observaciones)
            };

            unidad.Contexto.Ventas.Add(venta);
            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            foreach (var linea in solicitud.Lineas)
            {
                var producto = productos[linea.ProductoId];

                unidad.Contexto.VentaDetalles.Add(new VentaDetalle
                {
                    VentaId = venta.Id,
                    ProductoId = producto.Id,
                    DescripcionProducto = producto.Nombre,
                    Cantidad = linea.Cantidad,
                    PrecioUnitario = Dinero.Redondear(linea.PrecioUnitario),
                    CostoUnitario = Dinero.Redondear(linea.CostoUnitario),
                    PorcentajeDescuento = linea.PorcentajeDescuento,
                    PorcentajeIva = linea.PorcentajeIva,
                    ValorDescuento = linea.ValorDescuento,
                    ValorIva = linea.ValorIva,
                    Subtotal = linea.Subtotal,
                    Total = linea.Total
                });

                await _kardex.RegistrarAsync(
                    unidad, producto, TipoMovimientoKardex.VentaSalida,
                    linea.Cantidad, linea.CostoUnitario,
                    $"Venta a {cliente.Nombre}", venta.NumeroFactura, usuarioId, token)
                    .ConfigureAwait(false);
            }

            await _caja.RegistrarMovimientoDeVentaAsync(unidad, venta, usuarioId, token).ConfigureAwait(false);

            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            _log.LogInformation("Venta {Numero} registrada por {Total:C} ({Items} ítems, {Metodo})",
                venta.NumeroFactura, venta.Total, solicitud.Lineas.Count, venta.MetodoPago);

            return venta.Id;
        }, ct).ConfigureAwait(false);

        return await ObtenerDetalleInternoAsync(unidad, ventaId, ct).ConfigureAwait(false)
               ?? throw new NegocioException("La venta se registró pero no pudo recuperarse para imprimir.");
    }

    public async Task AnularAsync(int ventaId, string motivo, CancellationToken ct = default)
    {
        // Anular mueve inventario y dinero: se restringe al administrador.
        if (!_sesion.EsAdministrador)
        {
            throw new PermisoDenegadoException(
                "Solo un administrador puede anular facturas de venta.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new NegocioException("Escriba el motivo de la anulación.");
        }

        var usuarioId = _sesion.UsuarioIdRequerido;

        await using var unidad = _fabrica.Crear();

        var venta = await unidad.Contexto.Ventas
                        .Include(v => v.Detalles)
                        .FirstOrDefaultAsync(v => v.Id == ventaId, ct).ConfigureAwait(false)
                    ?? throw new RegistroNoEncontradoException("la venta", ventaId);

        if (venta.Estado == EstadoVenta.Anulada)
        {
            throw new NegocioException($"La factura {venta.NumeroFactura} ya estaba anulada.");
        }

        await unidad.EjecutarEnTransaccionAsync(async token =>
        {
            var idsProductos = venta.Detalles.Select(d => d.ProductoId).Distinct().ToList();

            var productos = await unidad.Contexto.Productos
                .Where(p => idsProductos.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, token).ConfigureAwait(false);

            foreach (var detalle in venta.Detalles)
            {
                if (!productos.TryGetValue(detalle.ProductoId, out var producto))
                {
                    continue;
                }

                await _kardex.RegistrarAsync(
                    unidad, producto, TipoMovimientoKardex.AnulacionVenta,
                    detalle.Cantidad, detalle.CostoUnitario,
                    $"Anulación de venta: {motivo}", venta.NumeroFactura, usuarioId, token)
                    .ConfigureAwait(false);
            }

            venta.Estado = EstadoVenta.Anulada;
            venta.FechaAnulacion = DateTime.Now;
            venta.MotivoAnulacion = Texto.Normalizar(motivo);
            venta.UsuarioAnulacionId = usuarioId;

            await _caja.RegistrarAnulacionDeVentaAsync(unidad, venta, usuarioId, token).ConfigureAwait(false);

            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            _log.LogWarning("Venta {Numero} anulada por {Usuario}: {Motivo}",
                venta.NumeroFactura, _sesion.Usuario?.NombreUsuario, motivo);

            return true;
        }, ct).ConfigureAwait(false);
    }

    public async Task<List<ProductoVendidoDto>> ObtenerMasVendidosAsync(
        DateTime desde, DateTime hasta, int cantidad = 10, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        var inicio = desde.Date;
        var fin = hasta.Date.AddDays(1);

        var consulta = unidad.Contexto.VentaDetalles
            .AsNoTracking()
            .Where(d => d.Venta!.Estado == EstadoVenta.Completada &&
                        d.Venta!.Fecha >= inicio && d.Venta!.Fecha < fin);

        // SQLite no puede ordenar por columnas decimal, así que la agregación y el
        // orden se resuelven en tipos double y la conversión a decimal se hace
        // ya con los datos en memoria.
        var agregado = await consulta
            .GroupBy(d => new { d.ProductoId, d.Producto!.Codigo, d.Producto!.Nombre })
            .Select(g => new
            {
                g.Key.ProductoId,
                g.Key.Codigo,
                g.Key.Nombre,
                Cantidad = g.Sum(d => (double)d.Cantidad),
                Monto = g.Sum(d => (double)(d.Subtotal - d.ValorDescuento)),
                Utilidad = g.Sum(d => (double)(d.Subtotal - d.ValorDescuento - d.Cantidad * d.CostoUnitario))
            })
            .OrderByDescending(g => g.Cantidad)
            .Take(cantidad)
            .ToListAsync(ct).ConfigureAwait(false);

        var ranking = agregado.Select(a => new ProductoVendidoDto
        {
            ProductoId = a.ProductoId,
            Codigo = a.Codigo,
            Nombre = a.Nombre,
            CantidadVendida = Dinero.Redondear(a.Cantidad),
            MontoVendido = Dinero.Redondear(a.Monto),
            Utilidad = Dinero.Redondear(a.Utilidad)
        }).ToList();

        var totalPeriodo = ranking.Sum(r => r.MontoVendido);

        foreach (var fila in ranking)
        {
            fila.Participacion = totalPeriodo == 0
                ? 0
                : Math.Round(fila.MontoVendido / totalPeriodo * 100m, 1);
        }

        return ranking;
    }

    private static void Validar(SolicitudVenta solicitud)
    {
        if (solicitud.ClienteId <= 0)
        {
            throw new NegocioException("Seleccione el cliente de la venta.");
        }

        if (solicitud.Lineas.Count == 0)
        {
            throw new NegocioException("Agregue al menos un producto antes de cobrar.");
        }

        foreach (var linea in solicitud.Lineas)
        {
            if (linea.Cantidad <= 0)
            {
                throw new NegocioException("Todas las cantidades deben ser mayores que cero.");
            }

            if (linea.PrecioUnitario < 0)
            {
                throw new NegocioException("El precio unitario no puede ser negativo.");
            }

            if (linea.PorcentajeDescuento is < 0 or > 100)
            {
                throw new NegocioException("El descuento debe estar entre 0 % y 100 %.");
            }
        }

        if (solicitud.Total <= 0)
        {
            throw new NegocioException("El total de la venta debe ser mayor que cero.");
        }

        // En efectivo el importe entregado debe alcanzar; en otros medios se asume el total exacto.
        if (solicitud.MetodoPago is MetodoPago.Efectivo or MetodoPago.Mixto &&
            solicitud.MontoRecibido < solicitud.Total && solicitud.MetodoPago == MetodoPago.Efectivo)
        {
            throw new NegocioException(
                $"El efectivo recibido ({solicitud.MontoRecibido:N2}) es menor que el total " +
                $"a pagar ({solicitud.Total:N2}).");
        }
    }
}
