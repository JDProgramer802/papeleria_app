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

/// <inheritdoc cref="IServicioCotizaciones" />
public class ServicioCotizaciones : IServicioCotizaciones
{
    private const int DiasValidezPorDefecto = 15;

    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IServicioVentas _ventas;
    private readonly IServicioDocumentos _documentos;
    private readonly IServicioConfiguracion _configuracion;
    private readonly IContextoSesion _sesion;
    private readonly ILogger<ServicioCotizaciones> _log;

    public ServicioCotizaciones(
        IUnidadDeTrabajoFactory fabrica,
        IServicioVentas ventas,
        IServicioDocumentos documentos,
        IServicioConfiguracion configuracion,
        IContextoSesion sesion,
        ILogger<ServicioCotizaciones> log)
    {
        _fabrica = fabrica;
        _ventas = ventas;
        _documentos = documentos;
        _configuracion = configuracion;
        _sesion = sesion;
        _log = log;
    }

    // ── Consulta ────────────────────────────────────────────────────────────

    public async Task<ResultadoPaginado<CotizacionResumenDto>> BuscarAsync(
        FiltroCotizaciones filtro, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Cotizaciones, AccionPermiso.Ver);

        await using var unidad = _fabrica.Crear();

        var hasta = filtro.Hasta.Date.AddDays(1);

        var consulta = unidad.Contexto.Cotizaciones
            .AsNoTracking()
            .Where(c => c.Fecha >= filtro.Desde.Date && c.Fecha < hasta);

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();
            consulta = consulta.Where(c =>
                EF.Functions.Like(c.Numero, $"%{texto}%") ||
                EF.Functions.Like(c.Cliente!.Nombre, $"%{texto}%") ||
                (c.Cliente.NumeroDocumento != null &&
                 EF.Functions.Like(c.Cliente.NumeroDocumento, $"%{texto}%")));
        }

        if (filtro.ClienteId is > 0)
        {
            consulta = consulta.Where(c => c.ClienteId == filtro.ClienteId);
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(c => c.Estado == estado);
        }

        if (filtro.SoloVigentes)
        {
            var hoy = DateTime.Today;
            consulta = consulta.Where(c => c.Estado == EstadoCotizacion.Vigente && c.FechaVence >= hoy);
        }

        var total = await consulta.CountAsync(ct).ConfigureAwait(false);

        var elementos = await consulta
            .OrderByDescending(c => c.Fecha)
            .Skip((Math.Max(filtro.Pagina, 1) - 1) * filtro.TamanoPagina)
            .Take(filtro.TamanoPagina)
            .Select(c => new CotizacionResumenDto
            {
                Id = c.Id,
                Numero = c.Numero,
                Fecha = c.Fecha,
                FechaVence = c.FechaVence,
                ClienteNombre = c.Cliente!.Nombre,
                UsuarioNombre = c.Usuario!.NombreCompleto,
                Estado = c.Estado,
                Total = c.Total,
                CantidadItems = c.Detalles.Count,
                VentaId = c.VentaId,
                NumeroFactura = c.Venta != null ? c.Venta.NumeroFactura : null
            })
            .ToListAsync(ct).ConfigureAwait(false);

        return new ResultadoPaginado<CotizacionResumenDto>(
            elementos, total, filtro.Pagina, filtro.TamanoPagina);
    }

    public async Task<CotizacionDetalladaDto?> ObtenerDetalleAsync(
        int cotizacionId, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Cotizaciones, AccionPermiso.Ver);

        await using var unidad = _fabrica.Crear();

        return await ObtenerDetalleInternoAsync(unidad, cotizacionId, ct).ConfigureAwait(false);
    }

    private static async Task<CotizacionDetalladaDto?> ObtenerDetalleInternoAsync(
        IUnidadDeTrabajo unidad, int cotizacionId, CancellationToken ct) =>
        await unidad.Contexto.Cotizaciones
            .AsNoTracking()
            .Where(c => c.Id == cotizacionId)
            .Select(c => new CotizacionDetalladaDto
            {
                Id = c.Id,
                Numero = c.Numero,
                Fecha = c.Fecha,
                FechaVence = c.FechaVence,
                ClienteId = c.ClienteId,
                ClienteNombre = c.Cliente!.Nombre,
                ClienteDocumento = c.Cliente.NumeroDocumento,
                ClienteTelefono = c.Cliente.Telefono,
                UsuarioNombre = c.Usuario!.NombreCompleto,
                Estado = c.Estado,
                Subtotal = c.Subtotal,
                TotalDescuento = c.TotalDescuento,
                TotalIva = c.TotalIva,
                Total = c.Total,
                Observaciones = c.Observaciones,
                CantidadItems = c.Detalles.Count,
                VentaId = c.VentaId,
                NumeroFactura = c.Venta != null ? c.Venta.NumeroFactura : null,
                Lineas = c.Detalles.Select(d => new CotizacionLineaDto
                {
                    ProductoId = d.ProductoId,
                    Codigo = d.Producto!.Codigo,
                    Descripcion = d.DescripcionProducto,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    PorcentajeDescuento = d.PorcentajeDescuento,
                    PorcentajeIva = d.PorcentajeIva,
                    Total = d.Total
                }).ToList()
            })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

    // ── Registro ────────────────────────────────────────────────────────────

    public async Task<CotizacionDetalladaDto> RegistrarAsync(
        SolicitudCotizacion solicitud, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Cotizaciones, AccionPermiso.Crear);

        if (solicitud.Lineas.Count == 0)
        {
            throw new NegocioException("La cotización no tiene renglones.");
        }

        if (solicitud.Lineas.Any(l => l.Cantidad <= 0))
        {
            throw new NegocioException("Todas las cantidades deben ser mayores que cero.");
        }

        var usuarioId = _sesion.UsuarioIdRequerido;

        var dias = solicitud.DiasValidez > 0
            ? solicitud.DiasValidez
            : _configuracion.ObtenerEntero(ClavesConfiguracion.CotizacionDiasValidez, DiasValidezPorDefecto);

        await using var unidad = _fabrica.Crear();

        var id = await unidad.EjecutarEnTransaccionAsync(async token =>
        {
            var cliente = await unidad.Contexto.Clientes
                              .FirstOrDefaultAsync(c => c.Id == solicitud.ClienteId, token).ConfigureAwait(false)
                          ?? throw new RegistroNoEncontradoException("el cliente", solicitud.ClienteId);

            var idsProducto = solicitud.Lineas.Select(l => l.ProductoId).Distinct().ToList();

            var productos = await unidad.Contexto.Productos
                .Where(p => idsProducto.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, token).ConfigureAwait(false);

            var numero = await _configuracion.ReservarConsecutivoAsync(
                unidad, ClavesConfiguracion.CotizacionPrefijo,
                ClavesConfiguracion.CotizacionConsecutivo, token).ConfigureAwait(false);

            var cotizacion = new Cotizacion
            {
                Numero = numero,
                Fecha = DateTime.Now,
                FechaVence = DateTime.Today.AddDays(Math.Max(dias, 1)),
                ClienteId = cliente.Id,
                UsuarioId = usuarioId,
                Estado = EstadoCotizacion.Vigente,
                Subtotal = solicitud.Subtotal,
                TotalDescuento = solicitud.TotalDescuento,
                TotalIva = solicitud.TotalIva,
                Total = solicitud.Total,
                Observaciones = string.IsNullOrWhiteSpace(solicitud.Observaciones)
                    ? null
                    : solicitud.Observaciones.Trim()
            };

            foreach (var linea in solicitud.Lineas)
            {
                if (!productos.TryGetValue(linea.ProductoId, out var producto))
                {
                    throw new NegocioException("Uno de los productos cotizados ya no existe.");
                }

                cotizacion.Detalles.Add(new CotizacionDetalle
                {
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
            }

            unidad.Contexto.Cotizaciones.Add(cotizacion);

            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            _log.LogInformation("Cotización {Numero} por {Total:C} para {Cliente}",
                cotizacion.Numero, cotizacion.Total, cliente.Nombre);

            return cotizacion.Id;
        }, ct).ConfigureAwait(false);

        return (await ObtenerDetalleInternoAsync(unidad, id, ct).ConfigureAwait(false))!;
    }

    // ── Conversión en venta ─────────────────────────────────────────────────

    public async Task<VentaDetalladaDto> ConvertirEnVentaAsync(
        int cotizacionId, SolicitudConversionCotizacion conversion, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Ventas, AccionPermiso.Crear);

        await using var unidad = _fabrica.Crear();

        var cotizacion = await unidad.Contexto.Cotizaciones
                             .Include(c => c.Detalles)
                             .FirstOrDefaultAsync(c => c.Id == cotizacionId, ct).ConfigureAwait(false)
                         ?? throw new RegistroNoEncontradoException("la cotización", cotizacionId);

        if (cotizacion.Estado == EstadoCotizacion.Aceptada)
        {
            throw new NegocioException(
                $"La cotización {cotizacion.Numero} ya se facturó.");
        }

        if (cotizacion.Estado == EstadoCotizacion.Rechazada)
        {
            throw new NegocioException(
                $"La cotización {cotizacion.Numero} está marcada como rechazada.");
        }

        // Vencida no impide facturar: el dueño decide si respeta el precio. Solo queda
        // constancia en el registro de que se facturó fuera de plazo.
        if (cotizacion.FechaVence.Date < DateTime.Today)
        {
            _log.LogInformation("La cotización {Numero} se facturó vencida (vencía el {Fecha:d})",
                cotizacion.Numero, cotizacion.FechaVence);
        }

        // La venta se registra por el camino de siempre: así descuenta existencias,
        // escribe el kardex y mueve la caja exactamente igual que cualquier otra.
        var venta = await _ventas.RegistrarAsync(new SolicitudVenta
        {
            ClienteId = cotizacion.ClienteId,
            MetodoPago = conversion.MetodoPago,
            MontoRecibido = conversion.MontoRecibido,
            ReferenciaPago = conversion.ReferenciaPago,
            Observaciones = string.IsNullOrWhiteSpace(conversion.Observaciones)
                ? $"Cotización {cotizacion.Numero}"
                : $"Cotización {cotizacion.Numero}. {conversion.Observaciones.Trim()}",
            Lineas = cotizacion.Detalles.Select(d => new LineaVenta
            {
                ProductoId = d.ProductoId,
                Cantidad = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                CostoUnitario = d.CostoUnitario,
                PorcentajeDescuento = d.PorcentajeDescuento,
                PorcentajeIva = d.PorcentajeIva
            }).ToList()
        }, ct).ConfigureAwait(false);

        cotizacion.Estado = EstadoCotizacion.Aceptada;
        cotizacion.VentaId = venta.Id;

        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

        _log.LogInformation("Cotización {Numero} facturada como {Factura}",
            cotizacion.Numero, venta.NumeroFactura);

        return venta;
    }

    public async Task RechazarAsync(int cotizacionId, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Cotizaciones, AccionPermiso.Editar);

        await using var unidad = _fabrica.Crear();

        var cotizacion = await unidad.Contexto.Cotizaciones
                             .FirstOrDefaultAsync(c => c.Id == cotizacionId, ct).ConfigureAwait(false)
                         ?? throw new RegistroNoEncontradoException("la cotización", cotizacionId);

        if (cotizacion.Estado == EstadoCotizacion.Aceptada)
        {
            throw new NegocioException(
                "La cotización ya se facturó: para deshacerla hay que anular la factura.");
        }

        cotizacion.Estado = EstadoCotizacion.Rechazada;

        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
    }

    public async Task<string> GenerarDocumentoAsync(int cotizacionId, CancellationToken ct = default)
    {
        var detalle = await ObtenerDetalleAsync(cotizacionId, ct).ConfigureAwait(false)
                      ?? throw new RegistroNoEncontradoException("la cotización", cotizacionId);

        return await _documentos.GenerarCotizacionAsync(detalle, null, ct).ConfigureAwait(false);
    }
}
