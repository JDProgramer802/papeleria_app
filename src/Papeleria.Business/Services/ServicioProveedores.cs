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

/// <inheritdoc cref="IServicioProveedores" />
public class ServicioProveedores : IServicioProveedores
{
    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IContextoSesion _sesion;

    public ServicioProveedores(IUnidadDeTrabajoFactory fabrica, IContextoSesion sesion)
    {
        _fabrica = fabrica;
        _sesion = sesion;
    }

    public async Task<ResultadoPaginado<Proveedor>> BuscarAsync(
        string? texto, bool soloActivos, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Proveedores, AccionPermiso.Ver);

        await using var unidad = _fabrica.Crear();

        var consulta = unidad.Contexto.Proveedores.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var termino = texto.Trim();
            consulta = consulta.Where(p =>
                EF.Functions.Like(p.Nombre, $"%{termino}%") ||
                (p.Nit != null && EF.Functions.Like(p.Nit, $"%{termino}%")) ||
                (p.Contacto != null && EF.Functions.Like(p.Contacto, $"%{termino}%")) ||
                (p.Telefono != null && EF.Functions.Like(p.Telefono, $"%{termino}%")) ||
                (p.Ciudad != null && EF.Functions.Like(p.Ciudad, $"%{termino}%")));
        }

        if (soloActivos)
        {
            consulta = consulta.Where(p => p.Activo);
        }

        var total = await consulta.CountAsync(ct).ConfigureAwait(false);

        var elementos = await consulta
            .OrderBy(p => p.Nombre)
            .Skip((Math.Max(pagina, 1) - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(ct).ConfigureAwait(false);

        return new ResultadoPaginado<Proveedor>(elementos, total, pagina, tamanoPagina);
    }

    public async Task<List<Proveedor>> ListarActivosAsync(CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        return await unidad.Contexto.Proveedores
            .AsNoTracking()
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<Proveedor?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();
        return await unidad.Contexto.Proveedores.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<Proveedor> CrearAsync(Proveedor proveedor, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Proveedores, AccionPermiso.Crear);

        await using var unidad = _fabrica.Crear();

        Normalizar(proveedor);
        await ValidarAsync(unidad, proveedor, null, ct).ConfigureAwait(false);

        var nuevo = new Proveedor();
        Copiar(proveedor, nuevo);

        unidad.Contexto.Proveedores.Add(nuevo);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

        return nuevo;
    }

    public async Task ActualizarAsync(Proveedor proveedor, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Proveedores, AccionPermiso.Editar);

        await using var unidad = _fabrica.Crear();

        var actual = await unidad.Contexto.Proveedores.FirstOrDefaultAsync(p => p.Id == proveedor.Id, ct)
                         .ConfigureAwait(false)
                     ?? throw new RegistroNoEncontradoException("el proveedor", proveedor.Id);

        Normalizar(proveedor);
        await ValidarAsync(unidad, proveedor, proveedor.Id, ct).ConfigureAwait(false);

        Copiar(proveedor, actual);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Proveedores, AccionPermiso.Eliminar);

        await using var unidad = _fabrica.Crear();

        var proveedor = await unidad.Contexto.Proveedores.FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false)
                        ?? throw new RegistroNoEncontradoException("el proveedor", id);

        if (await unidad.Contexto.Compras.AnyAsync(c => c.ProveedorId == id, ct).ConfigureAwait(false))
        {
            proveedor.Activo = false;
            await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

            throw new NegocioException(
                $"El proveedor «{proveedor.Nombre}» tiene compras registradas y no puede eliminarse. " +
                "Se marcó como inactivo.");
        }

        unidad.Contexto.Proveedores.Remove(proveedor);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
    }

    public async Task CambiarEstadoAsync(int id, bool activo, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Proveedores, AccionPermiso.Editar);

        await using var unidad = _fabrica.Crear();

        var proveedor = await unidad.Contexto.Proveedores.FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false)
                        ?? throw new RegistroNoEncontradoException("el proveedor", id);

        proveedor.Activo = activo;
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<CompraResumenDto>> ObtenerHistorialAsync(
        int proveedorId, int maximo = 200, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        return await unidad.Contexto.Compras
            .AsNoTracking()
            .Where(c => c.ProveedorId == proveedorId)
            .OrderByDescending(c => c.Fecha)
            .Take(maximo)
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
    }

    public async Task<ResumenTerceroDto> ObtenerResumenAsync(int proveedorId, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        var consulta = unidad.Contexto.Compras
            .AsNoTracking()
            .Where(c => c.ProveedorId == proveedorId && c.Estado == EstadoCompra.Registrada);

        if (!await consulta.AnyAsync(ct).ConfigureAwait(false))
        {
            return new ResumenTerceroDto();
        }

        return new ResumenTerceroDto
        {
            CantidadDocumentos = await consulta.CountAsync(ct).ConfigureAwait(false),
            MontoTotal = (decimal)await consulta.SumAsync(c => (double)c.Total, ct).ConfigureAwait(false),
            UltimaFecha = await consulta.MaxAsync(c => (DateTime?)c.Fecha, ct).ConfigureAwait(false)
        };
    }

    private static void Normalizar(Proveedor proveedor)
    {
        proveedor.Nombre = Texto.Normalizar(proveedor.Nombre);
        proveedor.Nit = Texto.NormalizarOpcional(proveedor.Nit);
        proveedor.Contacto = Texto.NormalizarOpcional(proveedor.Contacto);
        proveedor.Telefono = Texto.NormalizarOpcional(proveedor.Telefono);
        proveedor.Correo = Texto.NormalizarOpcional(proveedor.Correo);
        proveedor.Direccion = Texto.NormalizarOpcional(proveedor.Direccion);
        proveedor.Ciudad = Texto.NormalizarOpcional(proveedor.Ciudad);
        proveedor.Observaciones = Texto.NormalizarOpcional(proveedor.Observaciones);
    }

    private static async Task ValidarAsync(
        IUnidadDeTrabajo unidad, Proveedor proveedor, int? idExcluido, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(proveedor.Nombre))
        {
            throw new NegocioException("Escriba el nombre o la razón social del proveedor.");
        }

        if (!string.IsNullOrWhiteSpace(proveedor.Correo) && !proveedor.Correo.Contains('@'))
        {
            throw new NegocioException("El correo electrónico no tiene un formato válido.");
        }

        if (proveedor.Nit is not null &&
            await unidad.Contexto.Proveedores
                .AnyAsync(p => p.Nit == proveedor.Nit && (idExcluido == null || p.Id != idExcluido), ct)
                .ConfigureAwait(false))
        {
            throw new NegocioException($"Ya existe un proveedor registrado con el NIT «{proveedor.Nit}».");
        }
    }

    private static void Copiar(Proveedor origen, Proveedor destino)
    {
        destino.Nombre = origen.Nombre;
        destino.Nit = origen.Nit;
        destino.Contacto = origen.Contacto;
        destino.Telefono = origen.Telefono;
        destino.Correo = origen.Correo;
        destino.Direccion = origen.Direccion;
        destino.Ciudad = origen.Ciudad;
        destino.Observaciones = origen.Observaciones;
        destino.Activo = origen.Activo;
    }
}
