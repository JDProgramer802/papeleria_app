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

/// <inheritdoc cref="IServicioProductos" />
public class ServicioProductos : IServicioProductos
{
    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IContextoSesion _sesion;
    private readonly ILogger<ServicioProductos> _log;

    public ServicioProductos(
        IUnidadDeTrabajoFactory fabrica,
        IContextoSesion sesion,
        ILogger<ServicioProductos> log)
    {
        _fabrica = fabrica;
        _sesion = sesion;
        _log = log;
    }

    public async Task<ResultadoPaginado<ProductoListadoDto>> BuscarAsync(
        FiltroProductos filtro, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Productos, AccionPermiso.Ver);

        await using var unidad = _fabrica.Crear();

        var consulta = unidad.Contexto.Productos.AsNoTracking().AsQueryable();
        consulta = AplicarFiltros(consulta, filtro);

        var total = await consulta.CountAsync(ct).ConfigureAwait(false);

        consulta = AplicarOrden(consulta, filtro);

        var elementos = await consulta
            .Skip((Math.Max(filtro.Pagina, 1) - 1) * filtro.TamanoPagina)
            .Take(filtro.TamanoPagina)
            .Select(ProyeccionListado)
            .ToListAsync(ct).ConfigureAwait(false);

        return new ResultadoPaginado<ProductoListadoDto>(elementos, total, filtro.Pagina, filtro.TamanoPagina);
    }

    /// <summary>Proyección reutilizada en todos los listados de producto.</summary>
    private static System.Linq.Expressions.Expression<Func<Producto, ProductoListadoDto>> ProyeccionListado =>
        p => new ProductoListadoDto
        {
            Id = p.Id,
            Codigo = p.Codigo,
            CodigoBarras = p.CodigoBarras,
            Nombre = p.Nombre,
            Descripcion = p.Descripcion,
            CategoriaId = p.CategoriaId,
            CategoriaNombre = p.Categoria!.Nombre,
            MarcaId = p.MarcaId,
            MarcaNombre = p.Marca != null ? p.Marca.Nombre : string.Empty,
            UnidadMedidaId = p.UnidadMedidaId,
            UnidadAbreviatura = p.UnidadMedida!.Abreviatura,
            Costo = p.Costo,
            PrecioVenta = p.PrecioVenta,
            PorcentajeIva = p.PorcentajeIva,
            Tipo = p.Tipo,
            UnidadesPorPresentacion = p.UnidadesPorPresentacion,
            StockActual = p.StockActual,
            StockMinimo = p.StockMinimo,
            StockMaximo = p.StockMaximo,
            ImagenPath = p.ImagenPath,
            Ubicacion = p.Ubicacion,
            Activo = p.Activo
        };

    private static IQueryable<Producto> AplicarFiltros(IQueryable<Producto> consulta, FiltroProductos filtro)
    {
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();
            consulta = consulta.Where(p =>
                EF.Functions.Like(p.Nombre, $"%{texto}%") ||
                EF.Functions.Like(p.Codigo, $"%{texto}%") ||
                (p.CodigoBarras != null && EF.Functions.Like(p.CodigoBarras, $"%{texto}%")) ||
                (p.Descripcion != null && EF.Functions.Like(p.Descripcion, $"%{texto}%")));
        }

        if (filtro.CategoriaId is > 0)
        {
            consulta = consulta.Where(p => p.CategoriaId == filtro.CategoriaId);
        }

        if (filtro.MarcaId is > 0)
        {
            consulta = consulta.Where(p => p.MarcaId == filtro.MarcaId);
        }

        if (filtro.SoloActivos)
        {
            consulta = consulta.Where(p => p.Activo);
        }

        // El semáforo es una propiedad calculada del DTO; aquí se traduce a condiciones SQL.
        consulta = filtro.Estado switch
        {
            EstadoStock.Agotado => consulta.Where(p => p.StockActual <= 0),
            EstadoStock.Bajo => consulta.Where(p => p.StockActual > 0 && p.StockActual <= p.StockMinimo),
            EstadoStock.Normal => consulta.Where(p =>
                p.StockActual > p.StockMinimo && (p.StockMaximo <= 0 || p.StockActual <= p.StockMaximo)),
            EstadoStock.Exceso => consulta.Where(p => p.StockMaximo > 0 && p.StockActual > p.StockMaximo),
            _ => consulta
        };

        return consulta;
    }

    private static IQueryable<Producto> AplicarOrden(IQueryable<Producto> consulta, FiltroProductos filtro) =>
        (filtro.OrdenarPor, filtro.Descendente) switch
        {
            (nameof(ProductoListadoDto.Codigo), false) => consulta.OrderBy(p => p.Codigo),
            (nameof(ProductoListadoDto.Codigo), true) => consulta.OrderByDescending(p => p.Codigo),
            (nameof(ProductoListadoDto.PrecioVenta), false) => consulta.OrderBy(p => p.PrecioVenta),
            (nameof(ProductoListadoDto.PrecioVenta), true) => consulta.OrderByDescending(p => p.PrecioVenta),
            (nameof(ProductoListadoDto.Costo), false) => consulta.OrderBy(p => p.Costo),
            (nameof(ProductoListadoDto.Costo), true) => consulta.OrderByDescending(p => p.Costo),
            (nameof(ProductoListadoDto.StockActual), false) => consulta.OrderBy(p => p.StockActual),
            (nameof(ProductoListadoDto.StockActual), true) => consulta.OrderByDescending(p => p.StockActual),
            (nameof(ProductoListadoDto.CategoriaNombre), false) => consulta.OrderBy(p => p.Categoria!.Nombre),
            (nameof(ProductoListadoDto.CategoriaNombre), true) => consulta.OrderByDescending(p => p.Categoria!.Nombre),
            (_, true) => consulta.OrderByDescending(p => p.Nombre),
            _ => consulta.OrderBy(p => p.Nombre)
        };

    public async Task<Producto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        return await unidad.Contexto.Productos
            .AsNoTracking()
            .Include(p => p.Categoria)
            .Include(p => p.Marca)
            .Include(p => p.UnidadMedida)
            .FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<List<ProductoPosDto>> BuscarParaVentaAsync(
        string? texto, int maximo = 40, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        var consulta = unidad.Contexto.Productos.AsNoTracking().Where(p => p.Activo);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var termino = texto.Trim();
            consulta = consulta.Where(p =>
                EF.Functions.Like(p.Nombre, $"%{termino}%") ||
                EF.Functions.Like(p.Codigo, $"%{termino}%") ||
                (p.CodigoBarras != null && EF.Functions.Like(p.CodigoBarras, $"%{termino}%")));
        }

        return await consulta
            .OrderByDescending(p => p.StockActual > 0)
            .ThenBy(p => p.Nombre)
            .Take(maximo)
            .Select(ProyeccionPos)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    private static System.Linq.Expressions.Expression<Func<Producto, ProductoPosDto>> ProyeccionPos =>
        p => new ProductoPosDto
        {
            Id = p.Id,
            Codigo = p.Codigo,
            CodigoBarras = p.CodigoBarras,
            Nombre = p.Nombre,
            UnidadAbreviatura = p.UnidadMedida!.Abreviatura,
            CategoriaNombre = p.Categoria!.Nombre,
            PrecioVenta = p.PrecioVenta,
            Costo = p.Costo,
            PorcentajeIva = p.PorcentajeIva,
            Tipo = p.Tipo,
            StockActual = p.StockActual,
            ImagenPath = p.ImagenPath
        };

    public async Task<ProductoPosDto?> BuscarPorCodigoExactoAsync(string codigo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var termino = codigo.Trim();

        await using var unidad = _fabrica.Crear();

        return await unidad.Contexto.Productos
            .AsNoTracking()
            .Where(p => p.Activo && (p.CodigoBarras == termino || p.Codigo == termino))
            .Select(ProyeccionPos)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<Producto> CrearAsync(Producto producto, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Productos, AccionPermiso.Crear);

        await using var unidad = _fabrica.Crear();

        Normalizar(producto);
        await ValidarAsync(unidad, producto, null, ct).ConfigureAwait(false);

        var nuevo = new Producto();
        CopiarDatos(producto, nuevo);

        unidad.Contexto.Productos.Add(nuevo);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

        // Un stock inicial distinto de cero debe quedar justificado en el kardex.
        if (nuevo.StockActual != 0)
        {
            unidad.Contexto.MovimientosKardex.Add(new MovimientoKardex
            {
                ProductoId = nuevo.Id,
                Tipo = Domain.Enums.TipoMovimientoKardex.SaldoInicial,
                Cantidad = Math.Abs(nuevo.StockActual),
                Entrada = nuevo.StockActual > 0 ? nuevo.StockActual : 0,
                Salida = 0,
                StockAnterior = 0,
                StockNuevo = nuevo.StockActual,
                CostoUnitario = nuevo.Costo,
                UsuarioId = _sesion.UsuarioIdRequerido,
                Motivo = "Saldo inicial al crear el producto",
                DocumentoReferencia = nuevo.Codigo
            });

            await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
        }

        _log.LogInformation("Producto {Codigo} — {Nombre} creado", nuevo.Codigo, nuevo.Nombre);
        return nuevo;
    }

    public async Task ActualizarAsync(Producto producto, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Productos, AccionPermiso.Editar);

        await using var unidad = _fabrica.Crear();

        var actual = await unidad.Contexto.Productos.FirstOrDefaultAsync(p => p.Id == producto.Id, ct)
                         .ConfigureAwait(false)
                     ?? throw new RegistroNoEncontradoException("el producto", producto.Id);

        Normalizar(producto);
        await ValidarAsync(unidad, producto, producto.Id, ct).ConfigureAwait(false);

        // El stock nunca se edita aquí: solo cambia mediante compras, ventas o ajustes,
        // que son los que dejan rastro en el kardex.
        var stockOriginal = actual.StockActual;
        CopiarDatos(producto, actual);
        actual.StockActual = stockOriginal;

        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
        _log.LogInformation("Producto {Codigo} actualizado", actual.Codigo);
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Productos, AccionPermiso.Eliminar);

        await using var unidad = _fabrica.Crear();

        var producto = await unidad.Contexto.Productos.FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false)
                       ?? throw new RegistroNoEncontradoException("el producto", id);

        var tieneMovimientos =
            await unidad.Contexto.VentaDetalles.AnyAsync(d => d.ProductoId == id, ct).ConfigureAwait(false)
            || await unidad.Contexto.CompraDetalles.AnyAsync(d => d.ProductoId == id, ct).ConfigureAwait(false)
            || await unidad.Contexto.MovimientosKardex.AnyAsync(m => m.ProductoId == id, ct).ConfigureAwait(false);

        if (tieneMovimientos)
        {
            producto.Activo = false;
            await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

            throw new NegocioException(
                $"El producto «{producto.Nombre}» tiene movimientos registrados y no puede eliminarse " +
                "sin perder el histórico. Se marcó como inactivo.");
        }

        unidad.Contexto.Productos.Remove(producto);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

        _log.LogWarning("Producto {Codigo} eliminado por {Usuario}", producto.Codigo, _sesion.Usuario?.NombreUsuario);
    }

    public async Task CambiarEstadoAsync(int id, bool activo, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Productos, AccionPermiso.Editar);

        await using var unidad = _fabrica.Crear();

        var producto = await unidad.Contexto.Productos.FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false)
                       ?? throw new RegistroNoEncontradoException("el producto", id);

        producto.Activo = activo;
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
    }

    public async Task<Producto> DuplicarAsync(int id, CancellationToken ct = default)
    {
        _sesion.Exigir(Modulos.Productos, AccionPermiso.Crear);

        await using var unidad = _fabrica.Crear();

        var origen = await unidad.Contexto.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
                         .ConfigureAwait(false)
                     ?? throw new RegistroNoEncontradoException("el producto", id);

        // La copia nace sin existencias y sin código de barras: son datos únicos del original.
        var copia = new Producto
        {
            Codigo = await SugerirCodigoInternoAsync(unidad, ct).ConfigureAwait(false),
            CodigoBarras = null,
            Nombre = $"{origen.Nombre} (copia)",
            Descripcion = origen.Descripcion,
            CategoriaId = origen.CategoriaId,
            MarcaId = origen.MarcaId,
            UnidadMedidaId = origen.UnidadMedidaId,
            Costo = origen.Costo,
            PrecioVenta = origen.PrecioVenta,
            PorcentajeIva = origen.PorcentajeIva,
            StockActual = 0,
            StockMinimo = origen.StockMinimo,
            StockMaximo = origen.StockMaximo,
            Ubicacion = origen.Ubicacion,
            Observaciones = origen.Observaciones,
            ImagenPath = origen.ImagenPath,
            Activo = true
        };

        unidad.Contexto.Productos.Add(copia);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

        return copia;
    }

    public async Task<string> SugerirCodigoAsync(CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();
        return await SugerirCodigoInternoAsync(unidad, ct).ConfigureAwait(false);
    }

    private static async Task<string> SugerirCodigoInternoAsync(IUnidadDeTrabajo unidad, CancellationToken ct)
    {
        var codigos = await unidad.Contexto.Productos
            .AsNoTracking()
            .Where(p => p.Codigo.StartsWith("PRD-"))
            .Select(p => p.Codigo)
            .ToListAsync(ct).ConfigureAwait(false);

        var ultimo = codigos
            .Select(c => int.TryParse(c[4..], out var numero) ? numero : 0)
            .DefaultIfEmpty(0)
            .Max();

        return Texto.Consecutivo("PRD-", ultimo + 1, 4);
    }

    public async Task<string> GenerarCodigoBarrasAsync(CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        // Prefijo 770: rango asignado a Colombia por GS1. Se reintenta hasta hallar uno libre.
        for (var intento = 0; intento < 25; intento++)
        {
            var cuerpo = "770" + Random.Shared.Next(0, 1_000_000_000).ToString().PadLeft(9, '0');
            var candidato = cuerpo + CalcularDigitoVerificadorEan13(cuerpo);

            if (!await unidad.Contexto.Productos.AnyAsync(p => p.CodigoBarras == candidato, ct).ConfigureAwait(false))
            {
                return candidato;
            }
        }

        throw new NegocioException("No fue posible generar un código de barras libre. Inténtelo de nuevo.");
    }

    /// <summary>Dígito de control EAN-13: suma ponderada 1/3 de los 12 primeros dígitos.</summary>
    internal static int CalcularDigitoVerificadorEan13(string doceDigitos)
    {
        var suma = 0;

        for (var i = 0; i < doceDigitos.Length; i++)
        {
            var digito = doceDigitos[i] - '0';
            suma += i % 2 == 0 ? digito : digito * 3;
        }

        return (10 - suma % 10) % 10;
    }

    public async Task<List<ProductoListadoDto>> ListarBajoMinimoAsync(int maximo = 100, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        return await unidad.Contexto.Productos
            .AsNoTracking()
            .Where(p => p.Activo && p.Tipo == TipoProducto.Producto
                        && p.StockActual > 0 && p.StockActual <= p.StockMinimo)
            .OrderBy(p => p.StockActual)
            .Take(maximo)
            .Select(ProyeccionListado)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<ProductoListadoDto>> ListarAgotadosAsync(int maximo = 100, CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        return await unidad.Contexto.Productos
            .AsNoTracking()
            .Where(p => p.Activo && p.Tipo == TipoProducto.Producto && p.StockActual <= 0)
            .OrderBy(p => p.Nombre)
            .Take(maximo)
            .Select(ProyeccionListado)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    private static void Normalizar(Producto producto)
    {
        producto.Codigo = Texto.Normalizar(producto.Codigo).ToUpperInvariant();
        producto.Nombre = Texto.Normalizar(producto.Nombre);
        producto.Descripcion = Texto.NormalizarOpcional(producto.Descripcion);
        producto.Observaciones = Texto.NormalizarOpcional(producto.Observaciones);
        producto.Ubicacion = Texto.NormalizarOpcional(producto.Ubicacion);
        producto.ImagenPath = Texto.NormalizarOpcional(producto.ImagenPath);

        // Cadena vacía y null son lo mismo aquí; guardar "" rompería el índice único.
        producto.CodigoBarras = Texto.NormalizarOpcional(producto.CodigoBarras);

        if (producto.MarcaId is <= 0)
        {
            producto.MarcaId = null;
        }
    }

    private static async Task ValidarAsync(
        IUnidadDeTrabajo unidad, Producto producto, int? idExcluido, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(producto.Nombre))
        {
            throw new NegocioException("Escriba el nombre del producto.");
        }

        if (string.IsNullOrWhiteSpace(producto.Codigo))
        {
            throw new NegocioException("Escriba el código del producto.");
        }

        if (producto.CategoriaId <= 0)
        {
            throw new NegocioException("Seleccione la categoría del producto.");
        }

        if (producto.UnidadMedidaId <= 0)
        {
            throw new NegocioException("Seleccione la unidad de medida del producto.");
        }

        if (producto.Costo < 0 || producto.PrecioVenta < 0)
        {
            throw new NegocioException("El costo y el precio de venta no pueden ser negativos.");
        }

        if (producto.PorcentajeIva is < 0 or > 100)
        {
            throw new NegocioException("El porcentaje de IVA debe estar entre 0 y 100.");
        }

        if (producto.StockMinimo < 0 || producto.StockMaximo < 0)
        {
            throw new NegocioException("Los niveles de stock no pueden ser negativos.");
        }

        if (producto.StockMaximo > 0 && producto.StockMaximo < producto.StockMinimo)
        {
            throw new NegocioException("El stock máximo no puede ser menor que el stock mínimo.");
        }

        if (await unidad.Contexto.Productos
                .AnyAsync(p => p.Codigo == producto.Codigo && (idExcluido == null || p.Id != idExcluido), ct)
                .ConfigureAwait(false))
        {
            throw new NegocioException($"Ya existe un producto con el código «{producto.Codigo}».");
        }

        if (producto.CodigoBarras is not null &&
            await unidad.Contexto.Productos
                .AnyAsync(p => p.CodigoBarras == producto.CodigoBarras && (idExcluido == null || p.Id != idExcluido), ct)
                .ConfigureAwait(false))
        {
            throw new NegocioException($"El código de barras «{producto.CodigoBarras}» ya está asignado a otro producto.");
        }

        if (!await unidad.Contexto.Categorias.AnyAsync(c => c.Id == producto.CategoriaId, ct).ConfigureAwait(false))
        {
            throw new NegocioException("La categoría seleccionada ya no existe.");
        }

        if (!await unidad.Contexto.UnidadesMedida.AnyAsync(u => u.Id == producto.UnidadMedidaId, ct)
                .ConfigureAwait(false))
        {
            throw new NegocioException("La unidad de medida seleccionada ya no existe.");
        }
    }

    private static void CopiarDatos(Producto origen, Producto destino)
    {
        destino.Codigo = origen.Codigo;
        destino.CodigoBarras = origen.CodigoBarras;
        destino.Nombre = origen.Nombre;
        destino.Descripcion = origen.Descripcion;
        destino.CategoriaId = origen.CategoriaId;
        destino.MarcaId = origen.MarcaId;
        destino.UnidadMedidaId = origen.UnidadMedidaId;
        destino.Costo = Dinero.Redondear(origen.Costo);
        destino.PrecioVenta = Dinero.Redondear(origen.PrecioVenta);
        destino.PorcentajeIva = origen.PorcentajeIva;
        destino.Tipo = origen.Tipo;
        // Menos de una unidad por presentación no significa nada.
        destino.UnidadesPorPresentacion = Math.Max(origen.UnidadesPorPresentacion, 1);
        destino.StockActual = origen.StockActual;
        destino.StockMinimo = origen.StockMinimo;
        destino.StockMaximo = origen.StockMaximo;
        destino.ImagenPath = origen.ImagenPath;
        destino.Ubicacion = origen.Ubicacion;
        destino.Observaciones = origen.Observaciones;
        destino.Activo = origen.Activo;
    }
}
