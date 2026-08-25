using Microsoft.Extensions.DependencyInjection;
using Papeleria.App.ViewModels.Paginas;
using Papeleria.Business.Security;
using Papeleria.Domain.Constants;

namespace Papeleria.App.Infrastructure;

/// <inheritdoc cref="INavegacion" />
public class Navegacion : INavegacion
{
    /// <summary>Correspondencia entre la clave de cada módulo y su modelo de vista.</summary>
    private static readonly IReadOnlyDictionary<string, Type> Paginas = new Dictionary<string, Type>
    {
        [Modulos.Dashboard] = typeof(DashboardVistaModelo),
        [Modulos.Productos] = typeof(ProductosVistaModelo),
        [Modulos.Catalogos] = typeof(CatalogosVistaModelo),
        [Modulos.Proveedores] = typeof(ProveedoresVistaModelo),
        [Modulos.Clientes] = typeof(ClientesVistaModelo),
        [Modulos.Compras] = typeof(ComprasVistaModelo),
        [Modulos.Ventas] = typeof(PuntoVentaVistaModelo),
        [Modulos.HistorialVentas] = typeof(HistorialVentasVistaModelo),
        [Modulos.Cartera] = typeof(CarteraVistaModelo),
        [Modulos.Inventario] = typeof(InventarioVistaModelo),
        [Modulos.Kardex] = typeof(KardexVistaModelo),
        [Modulos.Caja] = typeof(CajaVistaModelo),
        [Modulos.Reportes] = typeof(ReportesVistaModelo),
        [Modulos.Configuracion] = typeof(ConfiguracionVistaModelo),
        [Modulos.Usuarios] = typeof(UsuariosVistaModelo),
        [Modulos.Manual] = typeof(ManualVistaModelo)
    };

    private readonly IServiceProvider _proveedor;
    private readonly IContextoSesion _sesion;
    private readonly Dictionary<string, PaginaVistaModelo> _cache = new(StringComparer.OrdinalIgnoreCase);

    public Navegacion(IServiceProvider proveedor, IContextoSesion sesion)
    {
        _proveedor = proveedor;
        _sesion = sesion;
    }

    public PaginaVistaModelo? PaginaActual { get; private set; }

    public event EventHandler<PaginaVistaModelo>? Navegado;

    public bool PuedeNavegar(string modulo) =>
        Paginas.ContainsKey(modulo) && _sesion.Puede(modulo);

    public async Task NavegarAsync(string modulo, object? parametro = null)
    {
        if (!PuedeNavegar(modulo))
        {
            return;
        }

        // Las páginas se conservan entre navegaciones para preservar filtros y
        // posición de scroll; solo se vuelven a cargar sus datos.
        if (!_cache.TryGetValue(modulo, out var pagina))
        {
            pagina = (PaginaVistaModelo)_proveedor.GetRequiredService(Paginas[modulo]);
            _cache[modulo] = pagina;
        }

        PaginaActual = pagina;
        Navegado?.Invoke(this, pagina);

        if (parametro is not null && pagina is IRecibeParametro receptor)
        {
            await receptor.RecibirParametroAsync(parametro).ConfigureAwait(true);
            return;
        }

        await pagina.CargarAsync().ConfigureAwait(true);
    }

    public Task RecargarAsync() =>
        PaginaActual is null ? Task.CompletedTask : PaginaActual.CargarAsync();
}
