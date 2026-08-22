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

/// <inheritdoc cref="IServicioCartera" />
public class ServicioCartera : IServicioCartera
{
    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IServicioCaja _caja;
    private readonly IContextoSesion _sesion;
    private readonly ILogger<ServicioCartera> _log;

    public ServicioCartera(
        IUnidadDeTrabajoFactory fabrica,
        IServicioCaja caja,
        IContextoSesion sesion,
        ILogger<ServicioCartera> log)
    {
        _fabrica = fabrica;
        _caja = caja;
        _sesion = sesion;
        _log = log;
    }

    // ── Consulta ────────────────────────────────────────────────────────────

    public async Task<ResultadoPaginado<SaldoClienteDto>> BuscarAsync(
        FiltroCartera filtro, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Cartera, AccionPermiso.Ver);

        await using var unidad = _fabrica.Crear();

        var saldos = await CalcularSaldosAsync(unidad, filtro, ct).ConfigureAwait(false);

        var total = saldos.Count;
        var pagina = Math.Max(filtro.Pagina, 1);

        var elementos = saldos
            .OrderByDescending(s => s.Saldo)
            .ThenBy(s => s.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .Skip((pagina - 1) * filtro.TamanoPagina)
            .Take(filtro.TamanoPagina)
            .ToList();

        return new ResultadoPaginado<SaldoClienteDto>(elementos, total, pagina, filtro.TamanoPagina);
    }

    public async Task<ResumenCarteraDto> ObtenerResumenAsync(
        FiltroCartera filtro, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Cartera, AccionPermiso.Ver);

        await using var unidad = _fabrica.Crear();

        var saldos = await CalcularSaldosAsync(unidad, filtro, ct).ConfigureAwait(false);

        // El vencido se reparte por la antigüedad de la factura pendiente más vieja:
        // es la manera en que se decide a quién hay que llamar primero.
        return new ResumenCarteraDto
        {
            ClientesConDeuda = saldos.Count(s => s.Saldo > 0),
            SaldoTotal = Dinero.Redondear(saldos.Sum(s => s.Saldo)),
            VencidoA30 = Dinero.Redondear(saldos.Where(s => s.DiasDeMora is > 0 and <= 30).Sum(s => s.Saldo)),
            VencidoA60 = Dinero.Redondear(saldos.Where(s => s.DiasDeMora is > 30 and <= 60).Sum(s => s.Saldo)),
            VencidoMas60 = Dinero.Redondear(saldos.Where(s => s.DiasDeMora > 60).Sum(s => s.Saldo))
        };
    }

    public async Task<SaldoClienteDto> ObtenerSaldoAsync(int clienteId, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();
        return await CalcularSaldoDeClienteAsync(unidad, clienteId, ct).ConfigureAwait(false);
    }

    public async Task<EstadoCuentaDto> ObtenerEstadoCuentaAsync(
        int clienteId, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Cartera, AccionPermiso.Ver);

        await using var unidad = _fabrica.Crear();

        var resumen = await CalcularSaldoDeClienteAsync(unidad, clienteId, ct).ConfigureAwait(false);

        var facturas = await unidad.Contexto.Ventas
            .AsNoTracking()
            .Where(v => v.ClienteId == clienteId
                        && v.MetodoPago == MetodoPago.Credito
                        && v.Estado == EstadoVenta.Completada)
            .OrderBy(v => v.Fecha).ThenBy(v => v.Id)
            .Select(v => new FacturaCreditoDto
            {
                VentaId = v.Id,
                NumeroFactura = v.NumeroFactura,
                Fecha = v.Fecha,
                Total = v.Total
            })
            .ToListAsync(ct).ConfigureAwait(false);

        AplicarAbonos(facturas, resumen.TotalAbonado);

        var abonos = await unidad.Contexto.AbonosCliente
            .AsNoTracking()
            .Where(a => a.ClienteId == clienteId)
            .OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id)
            .Select(a => new AbonoDto
            {
                Id = a.Id,
                ClienteId = a.ClienteId,
                ClienteNombre = a.Cliente!.Nombre,
                Fecha = a.Fecha,
                Monto = a.Monto,
                MetodoPago = a.MetodoPago,
                UsuarioNombre = a.Usuario!.NombreCompleto,
                Observaciones = a.Observaciones,
                Anulado = a.Anulado,
                MotivoAnulacion = a.MotivoAnulacion
            })
            .ToListAsync(ct).ConfigureAwait(false);

        return new EstadoCuentaDto { Resumen = resumen, Facturas = facturas, Abonos = abonos };
    }

    /// <summary>
    /// Reparte lo abonado entre las facturas, de la más antigua a la más reciente. El
    /// cliente abona a su cuenta, no a una factura concreta, así que ésta es la forma
    /// habitual de saber cuáles quedaron saldadas y cuál sigue pendiente.
    /// </summary>
    private static void AplicarAbonos(List<FacturaCreditoDto> facturas, decimal abonado)
    {
        var disponible = abonado;

        foreach (var factura in facturas)
        {
            if (disponible <= 0)
            {
                break;
            }

            var aplicado = Math.Min(disponible, factura.Total);

            factura.Aplicado = aplicado;
            disponible -= aplicado;
        }
    }

    // ── Cálculo de saldos ───────────────────────────────────────────────────

    /// <summary>
    /// Saldo de todos los clientes que alguna vez compraron a crédito. Las sumas se
    /// hacen en <c>double</c> porque SQLite no tiene decimal nativo y agregarlo daría error.
    /// </summary>
    private static async Task<List<SaldoClienteDto>> CalcularSaldosAsync(
        IUnidadDeTrabajo unidad, FiltroCartera filtro, CancellationToken ct)
    {
        var fiado = await unidad.Contexto.Ventas
            .AsNoTracking()
            .Where(v => v.MetodoPago == MetodoPago.Credito && v.Estado == EstadoVenta.Completada)
            .GroupBy(v => v.ClienteId)
            .Select(g => new
            {
                ClienteId = g.Key,
                Total = g.Sum(v => (double)v.Total),
                Facturas = g.Count(),
                MasAntigua = g.Min(v => v.Fecha)
            })
            .ToListAsync(ct).ConfigureAwait(false);

        if (fiado.Count == 0)
        {
            return new List<SaldoClienteDto>();
        }

        var idsClientes = fiado.Select(f => f.ClienteId).ToList();

        var abonado = await unidad.Contexto.AbonosCliente
            .AsNoTracking()
            .Where(a => !a.Anulado && idsClientes.Contains(a.ClienteId))
            .GroupBy(a => a.ClienteId)
            .Select(g => new { ClienteId = g.Key, Total = g.Sum(a => (double)a.Monto) })
            .ToListAsync(ct).ConfigureAwait(false);

        var clientes = await unidad.Contexto.Clientes
            .AsNoTracking()
            .Where(c => idsClientes.Contains(c.Id))
            .Select(c => new { c.Id, c.Nombre, c.NumeroDocumento, c.Telefono, c.LimiteCredito })
            .ToListAsync(ct).ConfigureAwait(false);

        var abonosPorCliente = abonado.ToDictionary(a => a.ClienteId, a => a.Total);
        var resultado = new List<SaldoClienteDto>(fiado.Count);

        foreach (var deuda in fiado)
        {
            var cliente = clientes.FirstOrDefault(c => c.Id == deuda.ClienteId);

            if (cliente is null)
            {
                continue;
            }

            resultado.Add(new SaldoClienteDto
            {
                ClienteId = deuda.ClienteId,
                Nombre = cliente.Nombre,
                NumeroDocumento = cliente.NumeroDocumento,
                Telefono = cliente.Telefono,
                TotalFiado = Dinero.Redondear(deuda.Total),
                TotalAbonado = Dinero.Redondear(
                    abonosPorCliente.TryGetValue(deuda.ClienteId, out var pagado) ? pagado : 0),
                LimiteCredito = cliente.LimiteCredito,
                FacturasPendientes = deuda.Facturas,
                DeudaMasAntigua = deuda.MasAntigua
            });
        }

        return Filtrar(resultado, filtro);
    }

    private static List<SaldoClienteDto> Filtrar(List<SaldoClienteDto> saldos, FiltroCartera filtro)
    {
        IEnumerable<SaldoClienteDto> consulta = saldos;

        if (filtro.SoloConSaldo)
        {
            consulta = consulta.Where(s => s.Saldo > 0);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();

            consulta = consulta.Where(s =>
                s.Nombre.Contains(texto, StringComparison.CurrentCultureIgnoreCase) ||
                (s.NumeroDocumento?.Contains(texto, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Telefono?.Contains(texto, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (filtro.DiasMoraMinimos is { } dias)
        {
            consulta = consulta.Where(s => s.DiasDeMora >= dias);
        }

        return consulta.ToList();
    }

    private static async Task<SaldoClienteDto> CalcularSaldoDeClienteAsync(
        IUnidadDeTrabajo unidad, int clienteId, CancellationToken ct)
    {
        var cliente = await unidad.Contexto.Clientes
                          .AsNoTracking()
                          .Select(c => new { c.Id, c.Nombre, c.NumeroDocumento, c.Telefono, c.LimiteCredito })
                          .FirstOrDefaultAsync(c => c.Id == clienteId, ct).ConfigureAwait(false)
                      ?? throw new RegistroNoEncontradoException("el cliente", clienteId);

        var deuda = await unidad.Contexto.Ventas
            .AsNoTracking()
            .Where(v => v.ClienteId == clienteId
                        && v.MetodoPago == MetodoPago.Credito
                        && v.Estado == EstadoVenta.Completada)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Sum(v => (double)v.Total),
                Facturas = g.Count(),
                MasAntigua = (DateTime?)g.Min(v => v.Fecha)
            })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        var abonado = await unidad.Contexto.AbonosCliente
            .AsNoTracking()
            .Where(a => a.ClienteId == clienteId && !a.Anulado)
            .SumAsync(a => (double?)a.Monto, ct).ConfigureAwait(false) ?? 0;

        return new SaldoClienteDto
        {
            ClienteId = cliente.Id,
            Nombre = cliente.Nombre,
            NumeroDocumento = cliente.NumeroDocumento,
            Telefono = cliente.Telefono,
            TotalFiado = Dinero.Redondear(deuda?.Total ?? 0),
            TotalAbonado = Dinero.Redondear(abonado),
            LimiteCredito = cliente.LimiteCredito,
            FacturasPendientes = deuda?.Facturas ?? 0,
            DeudaMasAntigua = deuda?.MasAntigua
        };
    }

    // ── Abonos ──────────────────────────────────────────────────────────────

    public async Task<AbonoDto> RegistrarAbonoAsync(
        SolicitudAbono solicitud, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Cartera, AccionPermiso.Crear);

        if (solicitud.Monto <= 0)
        {
            throw new NegocioException("El abono debe ser mayor que cero.");
        }

        var usuarioId = _sesion.UsuarioIdRequerido;

        await using var unidad = _fabrica.Crear();

        var saldo = await CalcularSaldoDeClienteAsync(unidad, solicitud.ClienteId, ct).ConfigureAwait(false);

        if (saldo.Saldo <= 0)
        {
            throw new NegocioException($"{saldo.Nombre} no tiene deuda pendiente.");
        }

        if (solicitud.Monto > saldo.Saldo)
        {
            throw new NegocioException(
                $"El abono ({Formatos.Moneda(solicitud.Monto)}) supera la deuda de " +
                $"{saldo.Nombre} ({Formatos.Moneda(saldo.Saldo)}).");
        }

        // El dinero en efectivo tiene que caer en un turno de caja para que el arqueo cuadre.
        var enEfectivo = solicitud.MetodoPago is MetodoPago.Efectivo;

        var sesionCaja = await unidad.Contexto.CajaSesiones
            .FirstOrDefaultAsync(s => s.Estado == EstadoCajaSesion.Abierta, ct).ConfigureAwait(false);

        if (enEfectivo && sesionCaja is null)
        {
            throw new NegocioException(
                "No hay una caja abierta. Ábrala antes de recibir abonos en efectivo.");
        }

        var abonoId = await unidad.EjecutarEnTransaccionAsync(async token =>
        {
            var abono = new AbonoCliente
            {
                ClienteId = solicitud.ClienteId,
                Fecha = DateTime.Now,
                Monto = Dinero.Redondear(solicitud.Monto),
                MetodoPago = solicitud.MetodoPago,
                UsuarioId = usuarioId,
                CajaSesionId = sesionCaja?.Id,
                Observaciones = Texto.NormalizarOpcional(solicitud.Observaciones)
            };

            unidad.Contexto.AbonosCliente.Add(abono);
            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            if (sesionCaja is not null)
            {
                await _caja.RegistrarAbonoDeClienteAsync(
                    unidad, abono, saldo.Nombre, sesionCaja.Id, usuarioId, token).ConfigureAwait(false);

                await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);
            }

            return abono.Id;
        }, ct).ConfigureAwait(false);

        _log.LogInformation(
            "Abono de {Monto} registrado a {Cliente} por {Usuario}",
            solicitud.Monto, saldo.Nombre, _sesion.Usuario?.NombreUsuario);

        return await ObtenerAbonoAsync(abonoId, ct).ConfigureAwait(false);
    }

    public async Task AnularAbonoAsync(int abonoId, string motivo, CancellationToken ct = default)
    {
        if (!_sesion.EsAdministrador)
        {
            throw new PermisoDenegadoException("anular abonos");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new NegocioException("Indique el motivo de la anulación.");
        }

        var usuarioId = _sesion.UsuarioIdRequerido;

        await using var unidad = _fabrica.Crear();

        var abono = await unidad.Contexto.AbonosCliente
                        .FirstOrDefaultAsync(a => a.Id == abonoId, ct).ConfigureAwait(false)
                    ?? throw new RegistroNoEncontradoException("el abono", abonoId);

        if (abono.Anulado)
        {
            throw new NegocioException("El abono ya estaba anulado.");
        }

        await unidad.EjecutarEnTransaccionAsync(async token =>
        {
            abono.Anulado = true;
            abono.FechaAnulacion = DateTime.Now;
            abono.MotivoAnulacion = motivo.Trim();

            // Si entró en efectivo, el dinero sale del cajón del turno abierto.
            if (abono.MetodoPago == MetodoPago.Efectivo)
            {
                var sesionCaja = await unidad.Contexto.CajaSesiones
                    .FirstOrDefaultAsync(s => s.Estado == EstadoCajaSesion.Abierta, token)
                    .ConfigureAwait(false);

                if (sesionCaja is not null)
                {
                    await _caja.RegistrarSalidaPorAbonoAnuladoAsync(
                        unidad, abono, sesionCaja.Id, usuarioId, token).ConfigureAwait(false);
                }
            }

            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);

        _log.LogWarning(
            "Abono {Id} anulado por {Usuario}: {Motivo}", abonoId, _sesion.Usuario?.NombreUsuario, motivo);
    }

    private async Task<AbonoDto> ObtenerAbonoAsync(int abonoId, CancellationToken ct)
    {
        await using var unidad = _fabrica.Crear();

        return await unidad.Contexto.AbonosCliente
                   .AsNoTracking()
                   .Where(a => a.Id == abonoId)
                   .Select(a => new AbonoDto
                   {
                       Id = a.Id,
                       ClienteId = a.ClienteId,
                       ClienteNombre = a.Cliente!.Nombre,
                       Fecha = a.Fecha,
                       Monto = a.Monto,
                       MetodoPago = a.MetodoPago,
                       UsuarioNombre = a.Usuario!.NombreCompleto,
                       Observaciones = a.Observaciones,
                       Anulado = a.Anulado,
                       MotivoAnulacion = a.MotivoAnulacion
                   })
                   .FirstOrDefaultAsync(ct).ConfigureAwait(false)
               ?? throw new RegistroNoEncontradoException("el abono", abonoId);
    }
}
