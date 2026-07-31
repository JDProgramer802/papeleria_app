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

/// <inheritdoc cref="IServicioCaja" />
public class ServicioCaja : IServicioCaja
{
    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IContextoSesion _sesion;
    private readonly ILogger<ServicioCaja> _log;

    public ServicioCaja(IUnidadDeTrabajoFactory fabrica, IContextoSesion sesion, ILogger<ServicioCaja> log)
    {
        _fabrica = fabrica;
        _sesion = sesion;
        _log = log;
    }

    public async Task<CajaSesion?> ObtenerSesionAbiertaAsync(CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        return await unidad.Contexto.CajaSesiones
            .AsNoTracking()
            .Include(s => s.UsuarioApertura)
            .Where(s => s.Estado == EstadoCajaSesion.Abierta)
            .OrderByDescending(s => s.FechaApertura)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> HayCajaAbiertaAsync(CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();
        return await unidad.Contexto.CajaSesiones
            .AnyAsync(s => s.Estado == EstadoCajaSesion.Abierta, ct).ConfigureAwait(false);
    }

    public async Task<CajaSesion> AbrirAsync(
        decimal montoInicial, string? observaciones, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Caja, AccionPermiso.Crear);

        if (montoInicial < 0)
        {
            throw new NegocioException("La base de caja no puede ser negativa.");
        }

        await using var unidad = _fabrica.Crear();

        if (await unidad.Contexto.CajaSesiones.AnyAsync(s => s.Estado == EstadoCajaSesion.Abierta, ct)
                .ConfigureAwait(false))
        {
            throw new NegocioException(
                "Ya hay una caja abierta. Debe cerrarla antes de abrir un nuevo turno.");
        }

        var usuarioId = _sesion.UsuarioIdRequerido;
        var monto = Dinero.Redondear(montoInicial);

        return await unidad.EjecutarEnTransaccionAsync(async token =>
        {
            var nueva = new CajaSesion
            {
                FechaApertura = DateTime.Now,
                UsuarioAperturaId = usuarioId,
                MontoInicial = monto,
                Estado = EstadoCajaSesion.Abierta,
                ObservacionesApertura = Texto.NormalizarOpcional(observaciones)
            };

            unidad.Contexto.CajaSesiones.Add(nueva);
            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            // La base inicial queda como primer movimiento, para que el historial cuadre.
            unidad.Contexto.MovimientosCaja.Add(new MovimientoCaja
            {
                CajaSesionId = nueva.Id,
                Fecha = nueva.FechaApertura,
                Tipo = TipoMovimientoCaja.Apertura,
                Monto = monto,
                Concepto = "Base inicial de caja",
                UsuarioId = usuarioId,
                AfectaEfectivo = true
            });

            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            _log.LogInformation("Caja abierta (sesión {Id}) con base {Monto:C} por {Usuario}",
                nueva.Id, monto, _sesion.Usuario?.NombreUsuario);

            return nueva;
        }, ct).ConfigureAwait(false);
    }

    public Task<MovimientoCaja> RegistrarIngresoAsync(
        decimal monto, string concepto, CancellationToken ct = default) =>
        RegistrarMovimientoManualAsync(TipoMovimientoCaja.Ingreso, monto, concepto, ct);

    public Task<MovimientoCaja> RegistrarEgresoAsync(
        decimal monto, string concepto, CancellationToken ct = default) =>
        RegistrarMovimientoManualAsync(TipoMovimientoCaja.Egreso, monto, concepto, ct);

    private async Task<MovimientoCaja> RegistrarMovimientoManualAsync(
        TipoMovimientoCaja tipo, decimal monto, string concepto, CancellationToken ct)
    {
        _sesion.Exigir(Modulos.Caja, AccionPermiso.Editar);

        if (monto <= 0)
        {
            throw new NegocioException("El monto debe ser mayor que cero.");
        }

        if (string.IsNullOrWhiteSpace(concepto))
        {
            throw new NegocioException("Escriba el concepto del movimiento.");
        }

        await using var unidad = _fabrica.Crear();

        var sesionCaja = await unidad.Contexto.CajaSesiones
                             .FirstOrDefaultAsync(s => s.Estado == EstadoCajaSesion.Abierta, ct)
                             .ConfigureAwait(false)
                         ?? throw new NegocioException("No hay una caja abierta para registrar el movimiento.");

        var importe = Dinero.Redondear(monto);

        if (tipo == TipoMovimientoCaja.Egreso)
        {
            var arqueo = await CalcularArqueoInternoAsync(unidad, sesionCaja, ct).ConfigureAwait(false);

            if (importe > arqueo.MontoEsperado)
            {
                throw new NegocioException(
                    $"El egreso ({importe:N2}) supera el efectivo disponible en caja ({arqueo.MontoEsperado:N2}).");
            }
        }

        var movimiento = new MovimientoCaja
        {
            CajaSesionId = sesionCaja.Id,
            Fecha = DateTime.Now,
            Tipo = tipo,
            Monto = importe,
            Concepto = Texto.Normalizar(concepto),
            UsuarioId = _sesion.UsuarioIdRequerido,
            AfectaEfectivo = true
        };

        unidad.Contexto.MovimientosCaja.Add(movimiento);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

        _log.LogInformation("{Tipo} de caja registrado: {Monto:C} — {Concepto}", tipo, importe, concepto);

        return movimiento;
    }

    public async Task RegistrarMovimientoDeVentaAsync(
        IUnidadDeTrabajo unidad, Venta venta, int usuarioId, CancellationToken ct = default)
    {
        if (venta.CajaSesionId is not { } sesionId)
        {
            return;
        }

        var efectivo = CalcularPorcionEfectivo(venta);

        unidad.Contexto.MovimientosCaja.Add(new MovimientoCaja
        {
            CajaSesionId = sesionId,
            Fecha = venta.Fecha,
            Tipo = TipoMovimientoCaja.Venta,
            Monto = venta.Total,
            Concepto = $"Venta {venta.NumeroFactura} ({venta.MetodoPago.Descripcion()})",
            UsuarioId = usuarioId,
            VentaId = venta.Id,
            // Solo el efectivo entra al cajón; tarjeta y transferencia no afectan el arqueo.
            AfectaEfectivo = efectivo > 0
        });

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task RegistrarAnulacionDeVentaAsync(
        IUnidadDeTrabajo unidad, Venta venta, int usuarioId, CancellationToken ct = default)
    {
        // La devolución se carga a la sesión abierta en el momento de anular; si no hay
        // ninguna, el ajuste queda solo en el histórico de la venta.
        var sesionAbierta = await unidad.Contexto.CajaSesiones
            .FirstOrDefaultAsync(s => s.Estado == EstadoCajaSesion.Abierta, ct).ConfigureAwait(false);

        if (sesionAbierta is null)
        {
            return;
        }

        var efectivo = CalcularPorcionEfectivo(venta);

        unidad.Contexto.MovimientosCaja.Add(new MovimientoCaja
        {
            CajaSesionId = sesionAbierta.Id,
            Fecha = DateTime.Now,
            Tipo = TipoMovimientoCaja.AnulacionVenta,
            Monto = venta.Total,
            Concepto = $"Anulación de la venta {venta.NumeroFactura}",
            UsuarioId = usuarioId,
            VentaId = venta.Id,
            AfectaEfectivo = efectivo > 0
        });
    }

    /// <summary>
    /// Parte de la venta que se cobró en efectivo. En pagos mixtos se toma el importe
    /// recibido en efectivo, sin superar el total de la factura.
    /// </summary>
    private static decimal CalcularPorcionEfectivo(Venta venta) => venta.MetodoPago switch
    {
        MetodoPago.Efectivo => venta.Total,
        MetodoPago.Mixto => Math.Min(Math.Max(venta.MontoRecibido - venta.Cambio, 0), venta.Total),
        _ => 0
    };

    public async Task<ArqueoCajaDto> CalcularArqueoAsync(int cajaSesionId, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Caja, AccionPermiso.Ver);

        await using var unidad = _fabrica.Crear();

        var sesionCaja = await unidad.Contexto.CajaSesiones
                             .Include(s => s.UsuarioApertura)
                             .FirstOrDefaultAsync(s => s.Id == cajaSesionId, ct).ConfigureAwait(false)
                         ?? throw new RegistroNoEncontradoException("la sesión de caja", cajaSesionId);

        return await CalcularArqueoInternoAsync(unidad, sesionCaja, ct).ConfigureAwait(false);
    }

    private static async Task<ArqueoCajaDto> CalcularArqueoInternoAsync(
        IUnidadDeTrabajo unidad, CajaSesion sesionCaja, CancellationToken ct)
    {
        var ventas = await unidad.Contexto.Ventas
            .AsNoTracking()
            .Where(v => v.CajaSesionId == sesionCaja.Id && v.Estado == EstadoVenta.Completada)
            .Select(v => new { v.MetodoPago, v.Total, v.MontoRecibido, v.Cambio })
            .ToListAsync(ct).ConfigureAwait(false);

        decimal efectivo = 0, tarjeta = 0, transferencia = 0, credito = 0;

        foreach (var venta in ventas)
        {
            switch (venta.MetodoPago)
            {
                case MetodoPago.Efectivo:
                    efectivo += venta.Total;
                    break;

                case MetodoPago.Tarjeta:
                    tarjeta += venta.Total;
                    break;

                case MetodoPago.Transferencia:
                    transferencia += venta.Total;
                    break;

                case MetodoPago.Credito:
                    credito += venta.Total;
                    break;

                case MetodoPago.Mixto:
                    var porcionEfectivo = Math.Min(Math.Max(venta.MontoRecibido - venta.Cambio, 0), venta.Total);
                    efectivo += porcionEfectivo;
                    tarjeta += venta.Total - porcionEfectivo;
                    break;
            }
        }

        var movimientos = await unidad.Contexto.MovimientosCaja
            .AsNoTracking()
            .Where(m => m.CajaSesionId == sesionCaja.Id)
            .Select(m => new { m.Tipo, m.Monto })
            .ToListAsync(ct).ConfigureAwait(false);

        var ingresos = movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Ingreso).Sum(m => m.Monto);
        var egresos = movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Egreso).Sum(m => m.Monto);

        // Las anulaciones de ventas cobradas en efectivo devuelven dinero del cajón.
        var anulaciones = movimientos.Where(m => m.Tipo == TipoMovimientoCaja.AnulacionVenta).Sum(m => m.Monto);

        return new ArqueoCajaDto
        {
            CajaSesionId = sesionCaja.Id,
            FechaApertura = sesionCaja.FechaApertura,
            UsuarioApertura = sesionCaja.UsuarioApertura?.NombreCompleto ?? string.Empty,
            MontoInicial = sesionCaja.MontoInicial,
            VentasEfectivo = Dinero.Redondear(efectivo),
            VentasTarjeta = Dinero.Redondear(tarjeta),
            VentasTransferencia = Dinero.Redondear(transferencia),
            VentasCredito = Dinero.Redondear(credito),
            Ingresos = Dinero.Redondear(ingresos),
            Egresos = Dinero.Redondear(egresos + anulaciones),
            CantidadVentas = ventas.Count
        };
    }

    public async Task<CajaSesion> CerrarAsync(
        int cajaSesionId, decimal montoReal, string? observaciones, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Caja, AccionPermiso.Editar);

        if (montoReal < 0)
        {
            throw new NegocioException("El efectivo contado no puede ser negativo.");
        }

        await using var unidad = _fabrica.Crear();

        var sesionCaja = await unidad.Contexto.CajaSesiones
                             .FirstOrDefaultAsync(s => s.Id == cajaSesionId, ct).ConfigureAwait(false)
                         ?? throw new RegistroNoEncontradoException("la sesión de caja", cajaSesionId);

        if (sesionCaja.Estado == EstadoCajaSesion.Cerrada)
        {
            throw new NegocioException("Esta sesión de caja ya fue cerrada.");
        }

        var usuarioId = _sesion.UsuarioIdRequerido;
        var contado = Dinero.Redondear(montoReal);

        return await unidad.EjecutarEnTransaccionAsync(async token =>
        {
            var arqueo = await CalcularArqueoInternoAsync(unidad, sesionCaja, token).ConfigureAwait(false);

            sesionCaja.FechaCierre = DateTime.Now;
            sesionCaja.UsuarioCierreId = usuarioId;
            sesionCaja.MontoEsperado = arqueo.MontoEsperado;
            sesionCaja.MontoReal = contado;
            sesionCaja.Diferencia = Dinero.Redondear(contado - arqueo.MontoEsperado);
            sesionCaja.TotalVentasEfectivo = arqueo.VentasEfectivo;
            sesionCaja.TotalVentasOtros = arqueo.VentasOtrosMedios;
            sesionCaja.TotalIngresos = arqueo.Ingresos;
            sesionCaja.TotalEgresos = arqueo.Egresos;
            sesionCaja.CantidadVentas = arqueo.CantidadVentas;
            sesionCaja.Estado = EstadoCajaSesion.Cerrada;
            sesionCaja.ObservacionesCierre = Texto.NormalizarOpcional(observaciones);

            unidad.Contexto.MovimientosCaja.Add(new MovimientoCaja
            {
                CajaSesionId = sesionCaja.Id,
                Fecha = sesionCaja.FechaCierre.Value,
                Tipo = TipoMovimientoCaja.Cierre,
                Monto = contado,
                Concepto = sesionCaja.Diferencia == 0
                    ? "Cierre de caja — cuadre exacto"
                    : $"Cierre de caja — {(sesionCaja.Diferencia < 0 ? "faltante" : "sobrante")} " +
                      $"de {Math.Abs(sesionCaja.Diferencia):N2}",
                UsuarioId = usuarioId,
                AfectaEfectivo = false
            });

            await unidad.GuardarCambiosAsync(token).ConfigureAwait(false);

            _log.LogInformation(
                "Caja {Id} cerrada. Esperado {Esperado:C}, contado {Contado:C}, diferencia {Diferencia:C}",
                sesionCaja.Id, sesionCaja.MontoEsperado, contado, sesionCaja.Diferencia);

            return sesionCaja;
        }, ct).ConfigureAwait(false);
    }

    public async Task<List<CajaSesionDto>> ListarSesionesAsync(
        DateTime? desde, DateTime? hasta, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Caja, AccionPermiso.Ver);

        await using var unidad = _fabrica.Crear();

        var consulta = unidad.Contexto.CajaSesiones.AsNoTracking().AsQueryable();

        if (desde is { } inicio)
        {
            var limite = inicio.Date;
            consulta = consulta.Where(s => s.FechaApertura >= limite);
        }

        if (hasta is { } fin)
        {
            var limite = fin.Date.AddDays(1);
            consulta = consulta.Where(s => s.FechaApertura < limite);
        }

        return await consulta
            .OrderByDescending(s => s.FechaApertura)
            .Select(ProyeccionSesion)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<CajaSesionDto?> ObtenerSesionAsync(int cajaSesionId, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        return await unidad.Contexto.CajaSesiones
            .AsNoTracking()
            .Where(s => s.Id == cajaSesionId)
            .Select(ProyeccionSesion)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    private static System.Linq.Expressions.Expression<Func<CajaSesion, CajaSesionDto>> ProyeccionSesion =>
        s => new CajaSesionDto
        {
            Id = s.Id,
            FechaApertura = s.FechaApertura,
            FechaCierre = s.FechaCierre,
            UsuarioApertura = s.UsuarioApertura!.NombreCompleto,
            UsuarioCierre = s.UsuarioCierre != null ? s.UsuarioCierre.NombreCompleto : string.Empty,
            MontoInicial = s.MontoInicial,
            MontoEsperado = s.MontoEsperado,
            MontoReal = s.MontoReal,
            Diferencia = s.Diferencia,
            TotalVentasEfectivo = s.TotalVentasEfectivo,
            TotalVentasOtros = s.TotalVentasOtros,
            TotalIngresos = s.TotalIngresos,
            TotalEgresos = s.TotalEgresos,
            CantidadVentas = s.CantidadVentas,
            Estado = s.Estado,
            ObservacionesApertura = s.ObservacionesApertura,
            ObservacionesCierre = s.ObservacionesCierre
        };

    public async Task<List<MovimientoCajaDto>> ObtenerMovimientosAsync(
        int cajaSesionId, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        return await unidad.Contexto.MovimientosCaja
            .AsNoTracking()
            .Where(m => m.CajaSesionId == cajaSesionId)
            .OrderBy(m => m.Fecha)
            .ThenBy(m => m.Id)
            .Select(m => new MovimientoCajaDto
            {
                Id = m.Id,
                Fecha = m.Fecha,
                Tipo = m.Tipo,
                Monto = m.Monto,
                Concepto = m.Concepto,
                UsuarioNombre = m.Usuario!.NombreCompleto,
                NumeroFactura = m.Venta != null ? m.Venta.NumeroFactura : null,
                AfectaEfectivo = m.AfectaEfectivo
            })
            .ToListAsync(ct).ConfigureAwait(false);
    }
}
