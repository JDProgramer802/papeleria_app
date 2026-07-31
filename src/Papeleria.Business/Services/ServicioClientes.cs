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

/// <inheritdoc cref="IServicioClientes" />
public class ServicioClientes : IServicioClientes
{
    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IContextoSesion _sesion;

    public ServicioClientes(IUnidadDeTrabajoFactory fabrica, IContextoSesion sesion)
    {
        _fabrica = fabrica;
        _sesion = sesion;
    }

    public async Task<ResultadoPaginado<Cliente>> BuscarAsync(
        string? texto, bool soloActivos, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Clientes, AccionPermiso.Ver);

        await using var unidad = _fabrica.Crear();

        var consulta = unidad.Contexto.Clientes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var termino = texto.Trim();
            consulta = consulta.Where(c =>
                EF.Functions.Like(c.Nombre, $"%{termino}%") ||
                (c.NumeroDocumento != null && EF.Functions.Like(c.NumeroDocumento, $"%{termino}%")) ||
                (c.Telefono != null && EF.Functions.Like(c.Telefono, $"%{termino}%")) ||
                (c.Correo != null && EF.Functions.Like(c.Correo, $"%{termino}%")));
        }

        if (soloActivos)
        {
            consulta = consulta.Where(c => c.Activo);
        }

        var total = await consulta.CountAsync(ct).ConfigureAwait(false);

        var elementos = await consulta
            .OrderByDescending(c => c.EsProtegido)
            .ThenBy(c => c.Nombre)
            .Skip((Math.Max(pagina, 1) - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(ct).ConfigureAwait(false);

        return new ResultadoPaginado<Cliente>(elementos, total, pagina, tamanoPagina);
    }

    public async Task<List<Cliente>> ListarActivosAsync(CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        return await unidad.Contexto.Clientes
            .AsNoTracking()
            .Where(c => c.Activo)
            .OrderByDescending(c => c.EsProtegido)
            .ThenBy(c => c.Nombre)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<Cliente> ObtenerConsumidorFinalAsync(CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        var cliente = await unidad.Contexto.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.EsProtegido, ct).ConfigureAwait(false);

        // Red de seguridad: si alguien borró el registro por fuera, se vuelve a crear.
        if (cliente is null)
        {
            cliente = new Cliente
            {
                Nombre = "Consumidor final",
                TipoDocumento = TipoDocumento.SinIdentificacion,
                NumeroDocumento = "222222222222",
                Activo = true,
                EsProtegido = true
            };

            unidad.Contexto.Clientes.Add(cliente);
            await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
        }

        return cliente;
    }

    public async Task<Cliente?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();
        return await unidad.Contexto.Clientes.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<Cliente> CrearAsync(Cliente cliente, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Clientes, AccionPermiso.Crear);

        await using var unidad = _fabrica.Crear();

        Normalizar(cliente);
        await ValidarAsync(unidad, cliente, null, ct).ConfigureAwait(false);

        var nuevo = new Cliente();
        Copiar(cliente, nuevo);
        nuevo.EsProtegido = false;

        unidad.Contexto.Clientes.Add(nuevo);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

        return nuevo;
    }

    public async Task ActualizarAsync(Cliente cliente, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Clientes, AccionPermiso.Editar);

        await using var unidad = _fabrica.Crear();

        var actual = await unidad.Contexto.Clientes.FirstOrDefaultAsync(c => c.Id == cliente.Id, ct)
                         .ConfigureAwait(false)
                     ?? throw new RegistroNoEncontradoException("el cliente", cliente.Id);

        Normalizar(cliente);
        await ValidarAsync(unidad, cliente, cliente.Id, ct).ConfigureAwait(false);

        if (actual.EsProtegido && !cliente.Activo)
        {
            throw new NegocioException("El cliente «Consumidor final» no puede desactivarse.");
        }

        Copiar(cliente, actual);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Clientes, AccionPermiso.Eliminar);

        await using var unidad = _fabrica.Crear();

        var cliente = await unidad.Contexto.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false)
                      ?? throw new RegistroNoEncontradoException("el cliente", id);

        if (cliente.EsProtegido)
        {
            throw new NegocioException("El cliente «Consumidor final» no puede eliminarse: lo usa el punto de venta.");
        }

        if (await unidad.Contexto.Ventas.AnyAsync(v => v.ClienteId == id, ct).ConfigureAwait(false))
        {
            cliente.Activo = false;
            await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

            throw new NegocioException(
                $"El cliente «{cliente.Nombre}» tiene ventas registradas y no puede eliminarse. " +
                "Se marcó como inactivo.");
        }

        unidad.Contexto.Clientes.Remove(cliente);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
    }

    public async Task CambiarEstadoAsync(int id, bool activo, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Clientes, AccionPermiso.Editar);

        await using var unidad = _fabrica.Crear();

        var cliente = await unidad.Contexto.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false)
                      ?? throw new RegistroNoEncontradoException("el cliente", id);

        if (cliente.EsProtegido && !activo)
        {
            throw new NegocioException("El cliente «Consumidor final» no puede desactivarse.");
        }

        cliente.Activo = activo;
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<VentaResumenDto>> ObtenerHistorialAsync(
        int clienteId, int maximo = 200, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        return await unidad.Contexto.Ventas
            .AsNoTracking()
            .Where(v => v.ClienteId == clienteId)
            .OrderByDescending(v => v.Fecha)
            .Take(maximo)
            .Select(v => new VentaResumenDto
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
            })
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<ResumenTerceroDto> ObtenerResumenAsync(int clienteId, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        var consulta = unidad.Contexto.Ventas
            .AsNoTracking()
            .Where(v => v.ClienteId == clienteId && v.Estado == EstadoVenta.Completada);

        if (!await consulta.AnyAsync(ct).ConfigureAwait(false))
        {
            return new ResumenTerceroDto();
        }

        return new ResumenTerceroDto
        {
            CantidadDocumentos = await consulta.CountAsync(ct).ConfigureAwait(false),
            MontoTotal = (decimal)await consulta.SumAsync(v => (double)v.Total, ct).ConfigureAwait(false),
            UltimaFecha = await consulta.MaxAsync(v => (DateTime?)v.Fecha, ct).ConfigureAwait(false)
        };
    }

    private static void Normalizar(Cliente cliente)
    {
        cliente.Nombre = Texto.Normalizar(cliente.Nombre);
        cliente.NumeroDocumento = Texto.NormalizarOpcional(cliente.NumeroDocumento);
        cliente.Telefono = Texto.NormalizarOpcional(cliente.Telefono);
        cliente.Correo = Texto.NormalizarOpcional(cliente.Correo);
        cliente.Direccion = Texto.NormalizarOpcional(cliente.Direccion);
        cliente.Ciudad = Texto.NormalizarOpcional(cliente.Ciudad);
        cliente.Observaciones = Texto.NormalizarOpcional(cliente.Observaciones);
    }

    private static async Task ValidarAsync(
        IUnidadDeTrabajo unidad, Cliente cliente, int? idExcluido, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cliente.Nombre))
        {
            throw new NegocioException("Escriba el nombre del cliente.");
        }

        if (!string.IsNullOrWhiteSpace(cliente.Correo) && !cliente.Correo.Contains('@'))
        {
            throw new NegocioException("El correo electrónico no tiene un formato válido.");
        }

        if (cliente.NumeroDocumento is not null &&
            await unidad.Contexto.Clientes
                .AnyAsync(c => c.NumeroDocumento == cliente.NumeroDocumento &&
                               (idExcluido == null || c.Id != idExcluido), ct)
                .ConfigureAwait(false))
        {
            throw new NegocioException(
                $"Ya existe un cliente con el documento «{cliente.NumeroDocumento}».");
        }
    }

    private static void Copiar(Cliente origen, Cliente destino)
    {
        destino.Nombre = origen.Nombre;
        destino.TipoDocumento = origen.TipoDocumento;
        destino.NumeroDocumento = origen.NumeroDocumento;
        destino.Telefono = origen.Telefono;
        destino.Correo = origen.Correo;
        destino.Direccion = origen.Direccion;
        destino.Ciudad = origen.Ciudad;
        destino.Observaciones = origen.Observaciones;
        destino.Activo = origen.Activo;
    }
}
