using Microsoft.EntityFrameworkCore;
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

/// <inheritdoc cref="IServicioKardex" />
public class ServicioKardex : IServicioKardex
{
    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IContextoSesion _sesion;

    public ServicioKardex(IUnidadDeTrabajoFactory fabrica, IContextoSesion sesion)
    {
        _fabrica = fabrica;
        _sesion = sesion;
    }

    /// <summary>Determina si un tipo de movimiento suma, resta o es neutro respecto al stock.</summary>
    private static int SignoDeStock(TipoMovimientoKardex tipo) => tipo switch
    {
        TipoMovimientoKardex.CompraEntrada => 1,
        TipoMovimientoKardex.EntradaManual => 1,
        TipoMovimientoKardex.AjustePositivo => 1,
        TipoMovimientoKardex.AnulacionVenta => 1,
        TipoMovimientoKardex.SaldoInicial => 1,

        TipoMovimientoKardex.VentaSalida => -1,
        TipoMovimientoKardex.SalidaManual => -1,
        TipoMovimientoKardex.AjusteNegativo => -1,
        TipoMovimientoKardex.AnulacionCompra => -1,

        // Una transferencia reubica mercancía sin alterar la existencia total.
        TipoMovimientoKardex.Transferencia => 0,
        _ => 0
    };

    public async Task<MovimientoKardex> RegistrarAsync(
        IUnidadDeTrabajo unidad,
        Producto producto,
        TipoMovimientoKardex tipo,
        decimal cantidad,
        decimal costoUnitario,
        string motivo,
        string? documentoReferencia,
        int usuarioId,
        CancellationToken ct = default)
    {
        if (cantidad <= 0)
        {
            throw new NegocioException("La cantidad del movimiento debe ser mayor que cero.");
        }

        var signo = SignoDeStock(tipo);
        var stockAnterior = producto.StockActual;
        var stockNuevo = stockAnterior + signo * cantidad;

        if (stockNuevo < 0)
        {
            throw new NegocioException(
                $"No hay existencias suficientes de «{producto.Nombre}». " +
                $"Disponible: {stockAnterior:N2}, solicitado: {cantidad:N2}.");
        }

        producto.StockActual = stockNuevo;

        var movimiento = new MovimientoKardex
        {
            Fecha = DateTime.Now,
            ProductoId = producto.Id,
            Tipo = tipo,
            Cantidad = cantidad,
            Entrada = signo > 0 ? cantidad : 0,
            Salida = signo < 0 ? cantidad : 0,
            StockAnterior = stockAnterior,
            StockNuevo = stockNuevo,
            CostoUnitario = Dinero.Redondear(costoUnitario),
            UsuarioId = usuarioId,
            Motivo = Texto.Normalizar(motivo),
            DocumentoReferencia = Texto.NormalizarOpcional(documentoReferencia)
        };

        unidad.Contexto.MovimientosKardex.Add(movimiento);

        return await Task.FromResult(movimiento).ConfigureAwait(false);
    }

    public async Task<ResultadoPaginado<MovimientoKardexDto>> BuscarAsync(
        FiltroKardex filtro, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Kardex, AccionPermiso.Ver);

        await using var unidad = _fabrica.Crear();

        var consulta = ConstruirConsulta(unidad, filtro);

        var total = await consulta.CountAsync(ct).ConfigureAwait(false);

        var elementos = await consulta
            .OrderByDescending(m => m.Fecha)
            .ThenByDescending(m => m.Id)
            .Skip((Math.Max(filtro.Pagina, 1) - 1) * filtro.TamanoPagina)
            .Take(filtro.TamanoPagina)
            .Select(Proyeccion)
            .ToListAsync(ct).ConfigureAwait(false);

        return new ResultadoPaginado<MovimientoKardexDto>(elementos, total, filtro.Pagina, filtro.TamanoPagina);
    }

    private static IQueryable<MovimientoKardex> ConstruirConsulta(IUnidadDeTrabajo unidad, FiltroKardex filtro)
    {
        var consulta = unidad.Contexto.MovimientosKardex.AsNoTracking().AsQueryable();

        if (filtro.ProductoId is > 0)
        {
            consulta = consulta.Where(m => m.ProductoId == filtro.ProductoId);
        }

        if (filtro.Desde is { } desde)
        {
            var inicio = desde.Date;
            consulta = consulta.Where(m => m.Fecha >= inicio);
        }

        if (filtro.Hasta is { } hasta)
        {
            // El filtro es inclusivo: se toma hasta el último instante del día indicado.
            var fin = hasta.Date.AddDays(1);
            consulta = consulta.Where(m => m.Fecha < fin);
        }

        if (filtro.Tipo is { } tipo)
        {
            consulta = consulta.Where(m => m.Tipo == tipo);
        }

        if (filtro.UsuarioId is > 0)
        {
            consulta = consulta.Where(m => m.UsuarioId == filtro.UsuarioId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var termino = filtro.Texto.Trim();
            consulta = consulta.Where(m =>
                EF.Functions.Like(m.Producto!.Nombre, $"%{termino}%") ||
                EF.Functions.Like(m.Producto!.Codigo, $"%{termino}%") ||
                EF.Functions.Like(m.Motivo, $"%{termino}%") ||
                (m.DocumentoReferencia != null && EF.Functions.Like(m.DocumentoReferencia, $"%{termino}%")));
        }

        return consulta;
    }

    private static System.Linq.Expressions.Expression<Func<MovimientoKardex, MovimientoKardexDto>> Proyeccion =>
        m => new MovimientoKardexDto
        {
            Id = m.Id,
            Fecha = m.Fecha,
            ProductoId = m.ProductoId,
            ProductoCodigo = m.Producto!.Codigo,
            ProductoNombre = m.Producto!.Nombre,
            UnidadAbreviatura = m.Producto!.UnidadMedida!.Abreviatura,
            Tipo = m.Tipo,
            Cantidad = m.Cantidad,
            Entrada = m.Entrada,
            Salida = m.Salida,
            StockAnterior = m.StockAnterior,
            StockNuevo = m.StockNuevo,
            CostoUnitario = m.CostoUnitario,
            UsuarioNombre = m.Usuario!.NombreCompleto,
            Motivo = m.Motivo,
            DocumentoReferencia = m.DocumentoReferencia
        };

    public async Task<List<MovimientoKardexDto>> ObtenerRecientesAsync(
        int cantidad = 10, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        return await unidad.Contexto.MovimientosKardex
            .AsNoTracking()
            .OrderByDescending(m => m.Fecha)
            .ThenByDescending(m => m.Id)
            .Take(cantidad)
            .Select(Proyeccion)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<MovimientoKardexDto>> ObtenerPorProductoAsync(
        int productoId, DateTime? desde = null, DateTime? hasta = null, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        var consulta = ConstruirConsulta(unidad, new FiltroKardex
        {
            ProductoId = productoId,
            Desde = desde,
            Hasta = hasta
        });

        return await consulta
            .OrderBy(m => m.Fecha)
            .ThenBy(m => m.Id)
            .Select(Proyeccion)
            .ToListAsync(ct).ConfigureAwait(false);
    }
}
