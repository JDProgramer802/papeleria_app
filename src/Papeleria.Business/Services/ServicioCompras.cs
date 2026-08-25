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

/// <inheritdoc cref="IServicioCompras" />
public class ServicioCompras : IServicioCompras
{
    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IServicioKardex _kardex;
    private readonly IServicioConfiguracion _configuracion;
    private readonly IContextoSesion _sesion;
    private readonly ILogger<ServicioCompras> _log;

    public ServicioCompras(
        IUnidadDeTrabajoFactory fabrica,
        IServicioKardex kardex,
        IServicioConfiguracion configuracion,
        IContextoSesion sesion,
        ILogger<ServicioCompras> log)
    {
        _fabrica = fabrica;
        _kardex = kardex;
        _configuracion = configuracion;
        _sesion = sesion;
        _log = log;
    }

    public async Task<ResultadoPaginado<CompraResumenDto>> BuscarAsync(
        FiltroCompras filtro, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Compras, AccionPermiso.Ver);

        await using var unidad = _fabrica.Crear();

        var consulta = unidad.Contexto.Compras.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var termino = filtro.Texto.Trim();
            consulta = consulta.Where(c =>
                EF.Functions.Like(c.Numero, $"%{termino}%") ||
                (c.NumeroFacturaProveedor != null && EF.Functions.Like(c.NumeroFacturaProveedor, $"%{termino}%")) ||
                EF.Functions.Like(c.Proveedor!.Nombre, $"%{termino}%"));
        }

        if (filtro.ProveedorId is > 0)
        {
            consulta = consulta.Where(c => c.ProveedorId == filtro.ProveedorId);
        }

        if (filtro.Desde is { } desde)
        {
            var inicio = desde.Date;
            consulta = consulta.Where(c => c.Fecha >= inicio);
        }

        if (filtro.Hasta is { } hasta)
        {
            var fin = hasta.Date.AddDays(1);
            consulta = consulta.Where(c => c.Fecha < fin);
        }

        if (!filtro.IncluirAnuladas)
        {
            consulta = consulta.Where(c => c.Estado == EstadoCompra.Registrada);
        }

        var total = await consulta.CountAsync(ct).ConfigureAwait(false);

        var elementos = await consulta
            .OrderByDescending(c => c.Fecha)
            .ThenByDescending(c => c.Id)
            .Skip((Math.Max(filtro.Pagina, 1) - 1) * filtro.TamanoPagina)
            .Take(filtro.TamanoPagina)
            .Select(c => new CompraResumenDto
            {
                Id = c.Id,
                Numero = c.Numero,
                NumeroFacturaProveedor = c.NumeroFacturaProveedor,
                Fecha = c.Fecha,
                ProveedorId = c.ProveedorId,
                ProveedorNombre = c.Proveedor!.Nombre,
                UsuarioNombre = c.Usuario!.NombreCompleto,
                Subtotal = c.Subtotal,
                TotalDescuento = c.TotalDescuento,
                TotalIva = c.TotalIva,
                Total = c.Total,
                CantidadItems = c.Detalles.Count,
                Estado = c.Estado
            })
            .ToListAsync(ct).ConfigureAwait(false);

        return new ResultadoPaginado<CompraResumenDto>(elementos, total, filtro.Pagina, filtro.TamanoPagina);
    }

    public async Task<CompraDetalladaDto?> ObtenerDetalleAsync(int compraId, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        return await unidad.Contexto.Compras
            .AsNoTracking()
            .Where(c => c.Id == compraId)
            .Select(c => new CompraDetalladaDto
            {
                Id = c.Id,
                Numero = c.Numero,
                NumeroFacturaProveedor = c.NumeroFacturaProveedor,
                Fecha = c.Fecha,
                ProveedorNombre = c.Proveedor!.Nombre,
                ProveedorNit = c.Proveedor!.Nit,
                ProveedorTelefono = c.Proveedor!.Telefono,
                UsuarioNombre = c.Usuario!.NombreCompleto,
                Subtotal = c.Subtotal,
                TotalDescuento = c.TotalDescuento,
                TotalIva = c.TotalIva,
                Total = c.Total,
                Estado = c.Estado,
                Observaciones = c.Observaciones,
                Lineas = c.Detalles.Select(d => new LineaDocumentoDto
                {
                    ProductoId = d.ProductoId,
                    Codigo = d.Producto!.Codigo,
                    Descripcion = d.DescripcionProducto,
                    UnidadAbreviatura = d.Producto!.UnidadMedida!.Abreviatura,
                    Cantidad = d.Cantidad,
                    ValorUnitario = d.CostoUnitario,
                    PorcentajeDescuento = d.PorcentajeDescuento,
                    PorcentajeIva = d.PorcentajeIva,
                    ValorDescuento = d.ValorDescuento,
                    ValorIva = d.ValorIva,
                    Subtotal = d.Subtotal,
                    Total = d.Total
                }).ToList()
            })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<CompraResumenDto> RegistrarAsync(SolicitudCompra solicitud, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Compras, AccionPermiso.Crear);
        Validar(solicitud);

        var usuarioId = _sesion.UsuarioIdRequerido;

        await using var unidad = _fabrica.Crear();

        var proveedor = await unidad.Contexto.Proveedores
                            .AsNoTracking()
                            .FirstOrDefaultAsync(p => p.Id == solicitud.ProveedorId, ct).ConfigureAwait(false)
                        ?? throw new NegocioException("Seleccione un proveedor válido.");

        return await unidad.EjecutarEnTransaccionAsync(async token =>
        {
            var numero = await _configuracion.ReservarConsecutivoAsync(
                unidad, ClavesConfiguracion.CompraPrefijo, ClavesConfiguracion.CompraConsecutivo, token)
                .ConfigureAwait(false);

            var compra = new Compra
            {
                Numero = numero,
                NumeroFacturaProveedor = Texto.NormalizarOpcional(solicitud.NumeroFacturaProveedor),
                Fecha = solicitud.Fecha,
                ProveedorId = solicitud.ProveedorId,
                UsuarioId = usuarioId,
                Subtotal = solicitud.Subtotal,
                TotalDescuento = solicitud.TotalDescuento,
                TotalIva = solicitud.TotalIva,
                Total = solicitud.Total,
                Estado = EstadoCompra.Registrada,
                Observaciones = Texto.NormalizarOpcional(solicitud.Observaciones)
            };

            unidad.Contexto.Compras.Add(compra);
            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            var idsProductos = solicitud.Lineas.Select(l => l.ProductoId).Distinct().ToList();

            var productos = await unidad.Contexto.Productos
                .Where(p => idsProductos.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, token).ConfigureAwait(false);

            foreach (var linea in solicitud.Lineas)
            {
                if (!productos.TryGetValue(linea.ProductoId, out var producto))
                {
                    throw new NegocioException("Uno de los productos de la compra ya no existe.");
                }

                if (!producto.ControlaExistencias)
                {
                    throw new NegocioException(
                        $"«{producto.Nombre}» es un servicio y no se compra a proveedores.");
                }

                // La caja de doce se compra como una y entra al inventario como doce.
                // El dinero de la línea no cambia; lo que cambia es en qué unidad se
                // guarda la existencia y, por tanto, el costo que le corresponde a cada una.
                var unidades = ConvertirAUnidades(linea, producto);

                // Costo neto por unidad: precio pactado menos el descuento de la línea.
                // El IVA no se capitaliza al costo porque se declara aparte.
                var costoNetoUnitario = unidades == 0
                    ? Dinero.Redondear(linea.CostoUnitario)
                    : Dinero.DividirSeguro(linea.BaseGravable, unidades);

                unidad.Contexto.CompraDetalles.Add(new CompraDetalle
                {
                    CompraId = compra.Id,
                    ProductoId = producto.Id,
                    DescripcionProducto = producto.Nombre,
                    Cantidad = unidades,
                    CostoUnitario = costoNetoUnitario,
                    PorcentajeDescuento = linea.PorcentajeDescuento,
                    PorcentajeIva = linea.PorcentajeIva,
                    ValorDescuento = linea.ValorDescuento,
                    ValorIva = linea.ValorIva,
                    Subtotal = linea.Subtotal,
                    Total = linea.Total
                });

                ActualizarCostoPromedio(producto, unidades, costoNetoUnitario);

                await _kardex.RegistrarAsync(
                    unidad, producto, TipoMovimientoKardex.CompraEntrada,
                    unidades, costoNetoUnitario,
                    $"Compra a {proveedor.Nombre}", compra.Numero, usuarioId, token)
                    .ConfigureAwait(false);
            }

            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            _log.LogInformation("Compra {Numero} registrada por {Total:C} ({Lineas} líneas)",
                compra.Numero, compra.Total, solicitud.Lineas.Count);

            return new CompraResumenDto
            {
                Id = compra.Id,
                Numero = compra.Numero,
                NumeroFacturaProveedor = compra.NumeroFacturaProveedor,
                Fecha = compra.Fecha,
                ProveedorId = proveedor.Id,
                ProveedorNombre = proveedor.Nombre,
                UsuarioNombre = _sesion.Usuario?.NombreCompleto ?? string.Empty,
                Subtotal = compra.Subtotal,
                TotalDescuento = compra.TotalDescuento,
                TotalIva = compra.TotalIva,
                Total = compra.Total,
                CantidadItems = solicitud.Lineas.Count,
                Estado = compra.Estado
            };
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Promedio ponderado: mezcla el inventario existente con la mercancía que entra.
    /// Si no había existencias, el costo nuevo reemplaza al anterior.
    /// </summary>
    /// <summary>
    /// Pasa la cantidad de la línea a unidades de venta. Sin presentación marcada, o
    /// con un producto que se compra y se vende igual, la cantidad no cambia.
    /// </summary>
    private static decimal ConvertirAUnidades(LineaCompra linea, Producto producto)
    {
        if (!linea.PorPresentacion || producto.UnidadesPorPresentacion <= 1)
        {
            return linea.Cantidad;
        }

        return Dinero.Redondear(linea.Cantidad * producto.UnidadesPorPresentacion);
    }

    private static void ActualizarCostoPromedio(Producto producto, decimal cantidadEntrante, decimal costoEntrante)
    {
        if (cantidadEntrante <= 0)
        {
            return;
        }

        var stockPrevio = producto.StockActual;

        if (stockPrevio <= 0)
        {
            producto.Costo = Dinero.Redondear(costoEntrante);
            return;
        }

        var valorPrevio = stockPrevio * producto.Costo;
        var valorEntrante = cantidadEntrante * costoEntrante;

        producto.Costo = Dinero.DividirSeguro(valorPrevio + valorEntrante, stockPrevio + cantidadEntrante);
    }

    public async Task AnularAsync(int compraId, string motivo, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Compras, AccionPermiso.Eliminar);

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new NegocioException("Escriba el motivo de la anulación.");
        }

        var usuarioId = _sesion.UsuarioIdRequerido;

        await using var unidad = _fabrica.Crear();

        var compra = await unidad.Contexto.Compras
                         .Include(c => c.Detalles)
                         .FirstOrDefaultAsync(c => c.Id == compraId, ct).ConfigureAwait(false)
                     ?? throw new RegistroNoEncontradoException("la compra", compraId);

        if (compra.Estado == EstadoCompra.Anulada)
        {
            throw new NegocioException($"La compra {compra.Numero} ya estaba anulada.");
        }

        await unidad.EjecutarEnTransaccionAsync(async token =>
        {
            var idsProductos = compra.Detalles.Select(d => d.ProductoId).Distinct().ToList();

            var productos = await unidad.Contexto.Productos
                .Where(p => idsProductos.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, token).ConfigureAwait(false);

            foreach (var detalle in compra.Detalles)
            {
                if (!productos.TryGetValue(detalle.ProductoId, out var producto))
                {
                    continue;
                }

                if (producto.StockActual < detalle.Cantidad)
                {
                    throw new NegocioException(
                        $"No se puede anular la compra: de «{producto.Nombre}» quedan {producto.StockActual:N2} " +
                        $"unidades y la compra ingresó {detalle.Cantidad:N2}. " +
                        "Parte de esa mercancía ya se vendió o se ajustó.");
                }

                await _kardex.RegistrarAsync(
                    unidad, producto, TipoMovimientoKardex.AnulacionCompra,
                    detalle.Cantidad, detalle.CostoUnitario,
                    $"Anulación de compra: {motivo}", compra.Numero, usuarioId, token)
                    .ConfigureAwait(false);
            }

            compra.Estado = EstadoCompra.Anulada;
            compra.Observaciones = string.IsNullOrWhiteSpace(compra.Observaciones)
                ? $"ANULADA: {motivo}"
                : $"{compra.Observaciones} | ANULADA: {motivo}";

            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            _log.LogWarning("Compra {Numero} anulada por {Usuario}: {Motivo}",
                compra.Numero, _sesion.Usuario?.NombreUsuario, motivo);

            return true;
        }, ct).ConfigureAwait(false);
    }

    private static void Validar(SolicitudCompra solicitud)
    {
        if (solicitud.ProveedorId <= 0)
        {
            throw new NegocioException("Seleccione el proveedor de la compra.");
        }

        if (solicitud.Lineas.Count == 0)
        {
            throw new NegocioException("Agregue al menos un producto a la compra.");
        }

        foreach (var linea in solicitud.Lineas)
        {
            if (linea.Cantidad <= 0)
            {
                throw new NegocioException("Todas las cantidades deben ser mayores que cero.");
            }

            if (linea.CostoUnitario < 0)
            {
                throw new NegocioException("El costo unitario no puede ser negativo.");
            }

            if (linea.PorcentajeDescuento is < 0 or > 100)
            {
                throw new NegocioException("El descuento debe estar entre 0 % y 100 %.");
            }

            if (linea.PorcentajeIva is < 0 or > 100)
            {
                throw new NegocioException("El IVA debe estar entre 0 % y 100 %.");
            }
        }

        if (solicitud.Total <= 0)
        {
            throw new NegocioException("El total de la compra debe ser mayor que cero.");
        }
    }
}
