using Microsoft.EntityFrameworkCore;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;
using Papeleria.Business.Security;
using Papeleria.Data.Repositories;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Entities;
using Papeleria.Domain.Enums;
using Papeleria.Domain.Exceptions;

namespace Papeleria.Business.Services;

public class ServicioGastos(IUnidadDeTrabajoFactory fabrica, IContextoSesion sesion) : IServicioGastos
{
    public async Task<int> RegistrarAsync(SolicitudGasto s, CancellationToken ct = default)
    {
        sesion.Exigir(Modulos.Gastos, AccionPermiso.Crear);
        ValidarTexto(s.Concepto, 250, "concepto");
        ValidarTexto(s.Categoria, 80, "categoría");
        if (s.Beneficiario?.Length > 150 || s.Referencia?.Length > 100)
            throw new NegocioException("El beneficiario admite 150 caracteres y la referencia 100.");
        var monto = Dinero.Redondear(s.Monto);
        if (monto <= 0 || monto > 999999999999m)
            throw new NegocioException("El monto debe estar entre 0,01 y 999.999.999.999.");
        if (s.Fecha.Date > DateTime.Today || s.Fecha.Year < 2000)
            throw new NegocioException("La fecha debe estar entre el año 2000 y hoy.");
        if (s.MetodoPago is not (MetodoPago.Efectivo or MetodoPago.Tarjeta or MetodoPago.Transferencia or MetodoPago.Nequi or MetodoPago.Daviplata))
            throw new NegocioException("Seleccione una forma de pago válida para el gasto.");

        await using var u = fabrica.Crear();
        return await u.EjecutarEnTransaccionAsync(async token =>
        {
            var gasto = new Gasto
            {
                Fecha = s.Fecha.Date, Concepto = s.Concepto.Trim(), Categoria = s.Categoria.Trim(),
                Monto = monto, MetodoPago = s.MetodoPago, UsuarioId = sesion.UsuarioIdRequerido,
                Beneficiario = Texto.NormalizarOpcional(s.Beneficiario), Referencia = Texto.NormalizarOpcional(s.Referencia)
            };
            if (s.MetodoPago == MetodoPago.Efectivo)
            {
                var caja = await u.Contexto.CajaSesiones.FirstOrDefaultAsync(c => c.Estado == EstadoCajaSesion.Abierta, token)
                    ?? throw new NegocioException("Abra la caja antes de registrar un gasto en efectivo.");
                var arqueo = await ServicioCaja.CalcularArqueoInternoAsync(u, caja, token);
                if (monto > arqueo.MontoEsperado)
                    throw new NegocioException("El gasto supera el efectivo disponible en caja.");
                gasto.MovimientoCaja = Movimiento(caja.Id, monto, TipoMovimientoCaja.Egreso, $"Gasto: {gasto.Concepto}");
            }
            u.Contexto.Gastos.Add(gasto);
            await u.GuardarCambiosAsync(token);
            return gasto.Id;
        }, ct);
    }

    public async Task AnularAsync(int id, string motivo, CancellationToken ct = default)
    {
        sesion.Exigir(Modulos.Gastos, AccionPermiso.Eliminar);
        ValidarTexto(motivo, 500, "motivo de anulación");
        await using var u = fabrica.Crear();
        await u.EjecutarEnTransaccionAsync(async token =>
        {
            var gasto = await u.Contexto.Gastos.FirstOrDefaultAsync(g => g.Id == id, token)
                ?? throw new RegistroNoEncontradoException("el gasto", id);
            if (gasto.Anulado) throw new NegocioException("Este gasto ya fue anulado.");
            if (gasto.MetodoPago == MetodoPago.Efectivo)
            {
                var caja = await u.Contexto.CajaSesiones.FirstOrDefaultAsync(c => c.Estado == EstadoCajaSesion.Abierta, token)
                    ?? throw new NegocioException("Abra la caja para registrar el reintegro del gasto anulado.");
                gasto.MovimientoReversion = Movimiento(caja.Id, gasto.Monto, TipoMovimientoCaja.Ingreso, $"Reintegro del gasto #{id}");
            }
            gasto.Anulado = true;
            gasto.MotivoAnulacion = motivo.Trim();
            gasto.FechaAnulacion = DateTime.Now;
            gasto.UsuarioAnulacionId = sesion.UsuarioIdRequerido;
            await u.GuardarCambiosAsync(token);
            return true;
        }, ct);
    }

    public async Task<IReadOnlyList<GastoDto>> BuscarAsync(FiltroGastos f, CancellationToken ct = default)
    {
        sesion.Exigir(Modulos.Gastos, AccionPermiso.Ver);
        if (f.Desde.Date > f.Hasta.Date || f.Hasta.Date == DateTime.MaxValue.Date)
            throw new NegocioException("Revise el rango de fechas: desde no puede superar hasta.");
        await using var u = fabrica.Crear();
        var fin = f.Hasta.Date.AddDays(1);
        var inicio = f.Desde.Date;
        var q = u.Contexto.Gastos.AsNoTracking().Where(g => g.Fecha >= inicio && g.Fecha < fin);
        if (!f.IncluirAnulados) q = q.Where(g => !g.Anulado);
        if (!string.IsNullOrWhiteSpace(f.Categoria)) q = q.Where(g => g.Categoria == f.Categoria);
        if (!string.IsNullOrWhiteSpace(f.Texto))
        {
            var t = f.Texto.Trim().ToLower();
            q = q.Where(g => g.Concepto.ToLower().Contains(t) || (g.Beneficiario != null && g.Beneficiario.ToLower().Contains(t))
                || (g.Referencia != null && g.Referencia.ToLower().Contains(t)));
        }
        return await q.OrderByDescending(g => g.Fecha).ThenByDescending(g => g.Id)
            .Select(g => new GastoDto(g.Id, g.Fecha, g.Concepto, g.Categoria, g.Monto, g.MetodoPago,
                g.Beneficiario, g.Referencia, g.Anulado, g.Usuario!.NombreCompleto, g.MotivoAnulacion)).ToListAsync(ct);
    }

    private MovimientoCaja Movimiento(int cajaId, decimal monto, TipoMovimientoCaja tipo, string concepto) => new()
    {
        CajaSesionId = cajaId, Monto = monto, Tipo = tipo, Concepto = concepto,
        UsuarioId = sesion.UsuarioIdRequerido, Fecha = DateTime.Now, AfectaEfectivo = true
    };

    private static void ValidarTexto(string texto, int maximo, string campo)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length > maximo)
            throw new NegocioException($"Escriba el {campo} (máximo {maximo} caracteres).");
    }
}
