using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Papeleria.App.Infrastructure;
using Papeleria.App.ViewModels.Dialogos;
using Papeleria.Business.Dtos;
using Papeleria.Business.Security;
using Papeleria.Business.Services;
using Papeleria.Business.Services.Catalogos;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Entities;

namespace Papeleria.App.ViewModels.Paginas;

/// <summary>Administración del maestro de productos.</summary>
public partial class ProductosVistaModelo : PaginaVistaModelo
{
    private readonly IServicioProductos _productos;
    private readonly IServicioCategorias _categorias;
    private readonly IServicioMarcas _marcas;
    private readonly IServicioUnidadesMedida _unidades;
    private readonly IServicioCodigoBarras _codigoBarras;
    private readonly IServicioDocumentos _documentos;
    private readonly IServicioArchivos _archivos;
    private readonly IServicioDialogos _dialogos;
    private readonly IContextoSesion _sesion;

    private List<Categoria> _catalogoCategorias = new();
    private List<Marca> _catalogoMarcas = new();
    private List<UnidadMedida> _catalogoUnidades = new();
    private CancellationTokenSource? _cancelacionBusqueda;

    public ProductosVistaModelo(
        IServicioProductos productos,
        IServicioCategorias categorias,
        IServicioMarcas marcas,
        IServicioUnidadesMedida unidades,
        IServicioCodigoBarras codigoBarras,
        IServicioDocumentos documentos,
        IServicioArchivos archivos,
        IServicioDialogos dialogos,
        IContextoSesion sesion)
    {
        _productos = productos;
        _categorias = categorias;
        _marcas = marcas;
        _unidades = unidades;
        _codigoBarras = codigoBarras;
        _documentos = documentos;
        _archivos = archivos;
        _dialogos = dialogos;
        _sesion = sesion;

        Titulo = "Productos";
        Subtitulo = "Catálogo de artículos, precios y niveles de existencias";

        Paginador.PaginaCambiada += (_, _) => _ = BuscarAsync();
    }

    public override string Modulo => Modulos.Productos;

    public Paginador Paginador { get; } = new();

    public ObservableCollection<ProductoListadoDto> Productos { get; } = new();

    public ObservableCollection<Categoria> FiltroCategorias { get; } = new();

    public ObservableCollection<Marca> FiltroMarcas { get; } = new();

    /// <summary>Opciones del filtro por semáforo de existencias.</summary>
    public IReadOnlyList<KeyValuePair<EstadoStock?, string>> EstadosStock { get; } =
        new List<KeyValuePair<EstadoStock?, string>>
        {
            new(null, "Todas las existencias"),
            new(EstadoStock.Agotado, "Agotados"),
            new(EstadoStock.Bajo, "Bajo el mínimo"),
            new(EstadoStock.Normal, "Existencias normales"),
            new(EstadoStock.Exceso, "Sobre el máximo")
        };

    [ObservableProperty] private string? _textoBusqueda;
    [ObservableProperty] private int? _categoriaSeleccionadaId;
    [ObservableProperty] private int? _marcaSeleccionadaId;
    [ObservableProperty] private EstadoStock? _estadoSeleccionado;
    [ObservableProperty] private bool _soloActivos = true;
    [ObservableProperty] private ProductoListadoDto? _productoSeleccionado;

    public bool PuedeCrear => _sesion.Puede(Modulos.Productos, AccionPermiso.Crear);

    public bool PuedeEditar => _sesion.Puede(Modulos.Productos, AccionPermiso.Editar);

    public bool PuedeEliminar => _sesion.Puede(Modulos.Productos, AccionPermiso.Eliminar);

    public bool HaySeleccion => ProductoSeleccionado is not null;

    partial void OnProductoSeleccionadoChanged(ProductoListadoDto? value)
    {
        OnPropertyChanged(nameof(HaySeleccion));
        EditarCommand.NotifyCanExecuteChanged();
        DuplicarCommand.NotifyCanExecuteChanged();
        EliminarCommand.NotifyCanExecuteChanged();
        ImprimirEtiquetaCommand.NotifyCanExecuteChanged();
        AlternarEstadoCommand.NotifyCanExecuteChanged();
    }

    // Cualquier cambio de filtro reinicia la paginación y vuelve a consultar.
    partial void OnTextoBusquedaChanged(string? value) => ReiniciarBusqueda();
    partial void OnCategoriaSeleccionadaIdChanged(int? value) => ReiniciarBusqueda();
    partial void OnMarcaSeleccionadaIdChanged(int? value) => ReiniciarBusqueda();
    partial void OnEstadoSeleccionadoChanged(EstadoStock? value) => ReiniciarBusqueda();
    partial void OnSoloActivosChanged(bool value) => ReiniciarBusqueda();

    private void ReiniciarBusqueda()
    {
        Paginador.Reiniciar();
        _ = BuscarConRetrasoAsync();
    }

    /// <summary>
    /// Espera un instante antes de consultar para no lanzar una búsqueda por cada
    /// tecla mientras el usuario escribe.
    /// </summary>
    private async Task BuscarConRetrasoAsync()
    {
        _cancelacionBusqueda?.Cancel();
        _cancelacionBusqueda = new CancellationTokenSource();
        var token = _cancelacionBusqueda.Token;

        try
        {
            await Task.Delay(280, token).ConfigureAwait(true);

            if (!token.IsCancellationRequested)
            {
                await BuscarAsync().ConfigureAwait(true);
            }
        }
        catch (TaskCanceledException)
        {
            // Llegó otra pulsación: esta búsqueda se descarta.
        }
    }

    public override async Task CargarAsync()
    {
        await CargarCatalogosAsync().ConfigureAwait(true);
        await BuscarAsync().ConfigureAwait(true);
    }

    private Task CargarCatalogosAsync() => EjecutarAsync(async () =>
    {
        _catalogoCategorias = await _categorias.ListarAsync().ConfigureAwait(true);
        _catalogoMarcas = await _marcas.ListarAsync().ConfigureAwait(true);
        _catalogoUnidades = await _unidades.ListarAsync().ConfigureAwait(true);

        FiltroCategorias.Clear();
        FiltroCategorias.Add(new Categoria { Id = 0, Nombre = "Todas las categorías" });

        foreach (var categoria in _catalogoCategorias)
        {
            FiltroCategorias.Add(categoria);
        }

        FiltroMarcas.Clear();
        FiltroMarcas.Add(new Marca { Id = 0, Nombre = "Todas las marcas" });

        foreach (var marca in _catalogoMarcas)
        {
            FiltroMarcas.Add(marca);
        }
    }, "No se pudieron cargar los catálogos.");

    [RelayCommand]
    private Task BuscarAsync() => EjecutarAsync(async () =>
    {
        var filtro = new FiltroProductos
        {
            Texto = TextoBusqueda,
            CategoriaId = CategoriaSeleccionadaId,
            MarcaId = MarcaSeleccionadaId,
            Estado = EstadoSeleccionado,
            SoloActivos = SoloActivos,
            Pagina = Paginador.Pagina,
            TamanoPagina = Paginador.TamanoPagina
        };

        var resultado = await _productos.BuscarAsync(filtro).ConfigureAwait(true);

        Productos.Clear();

        foreach (var producto in resultado.Elementos)
        {
            Productos.Add(producto);
        }

        Paginador.Actualizar(resultado);
    }, "No se pudo consultar el catálogo de productos.");

    [RelayCommand]
    private async Task NuevoAsync()
    {
        if (!PuedeCrear)
        {
            return;
        }

        var codigoSugerido = await _productos.SugerirCodigoAsync().ConfigureAwait(true);

        var nuevo = new Producto
        {
            Codigo = codigoSugerido,
            Activo = true,
            PorcentajeIva = 19m
        };

        await AbrirFormularioAsync(nuevo, esNuevo: true).ConfigureAwait(true);
    }

    private bool PuedeOperarSobreSeleccion() => ProductoSeleccionado is not null;

    [RelayCommand(CanExecute = nameof(PuedeOperarSobreSeleccion))]
    private async Task EditarAsync()
    {
        if (ProductoSeleccionado is null || !PuedeEditar)
        {
            return;
        }

        var producto = await _productos.ObtenerAsync(ProductoSeleccionado.Id).ConfigureAwait(true);

        if (producto is null)
        {
            await _dialogos.InformarAsync("Producto no encontrado",
                "El producto ya no existe. Se actualizará el listado.", esError: true).ConfigureAwait(true);

            await BuscarAsync().ConfigureAwait(true);
            return;
        }

        await AbrirFormularioAsync(producto, esNuevo: false).ConfigureAwait(true);
    }

    private async Task AbrirFormularioAsync(Producto producto, bool esNuevo)
    {
        if (_catalogoCategorias.Count == 0 || _catalogoUnidades.Count == 0)
        {
            await CargarCatalogosAsync().ConfigureAwait(true);
        }

        if (_catalogoCategorias.Count == 0 || _catalogoUnidades.Count == 0)
        {
            await _dialogos.InformarAsync(
                "Faltan datos maestros",
                "Debe existir al menos una categoría y una unidad de medida antes de crear productos.",
                esError: true).ConfigureAwait(true);

            return;
        }

        var dialogo = new ProductoDialogoVistaModelo(
            _productos, _codigoBarras, _archivos, _dialogos,
            producto, esNuevo, _catalogoCategorias, _catalogoMarcas, _catalogoUnidades);

        var resultado = await _dialogos.MostrarAsync(dialogo).ConfigureAwait(true);

        if (resultado is not true)
        {
            return;
        }

        _dialogos.Notificar(esNuevo
            ? $"Producto «{producto.Nombre}» creado correctamente."
            : $"Producto «{producto.Nombre}» actualizado.");

        WeakReferenceMessenger.Default.Send(new InventarioCambiadoMensaje(producto.Id));

        await BuscarAsync().ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(PuedeOperarSobreSeleccion))]
    private async Task DuplicarAsync()
    {
        if (ProductoSeleccionado is null || !PuedeCrear)
        {
            return;
        }

        var confirmado = await _dialogos.ConfirmarAsync(
            "Duplicar producto",
            $"Se creará una copia de «{ProductoSeleccionado.Nombre}» sin existencias " +
            "y con un código nuevo. ¿Desea continuar?",
            "Duplicar").ConfigureAwait(true);

        if (!confirmado)
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            var copia = await _productos.DuplicarAsync(ProductoSeleccionado.Id).ConfigureAwait(true);

            _dialogos.Notificar($"Se creó la copia «{copia.Nombre}» con el código {copia.Codigo}.");

            await BuscarAsync().ConfigureAwait(true);
        }, "No se pudo duplicar el producto.");
    }

    [RelayCommand(CanExecute = nameof(PuedeOperarSobreSeleccion))]
    private async Task EliminarAsync()
    {
        if (ProductoSeleccionado is null || !PuedeEliminar)
        {
            return;
        }

        var confirmado = await _dialogos.ConfirmarAsync(
            "Eliminar producto",
            $"¿Seguro que desea eliminar «{ProductoSeleccionado.Nombre}»?\n\n" +
            "Si el producto ya tiene movimientos, se marcará como inactivo en lugar de borrarse " +
            "para no perder el histórico.",
            "Eliminar", esDestructivo: true).ConfigureAwait(true);

        if (!confirmado)
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            await _productos.EliminarAsync(ProductoSeleccionado.Id).ConfigureAwait(true);
            _dialogos.Notificar("Producto eliminado.");
            await BuscarAsync().ConfigureAwait(true);
        }, "No se pudo eliminar el producto.");
    }

    [RelayCommand(CanExecute = nameof(PuedeOperarSobreSeleccion))]
    private async Task AlternarEstadoAsync()
    {
        if (ProductoSeleccionado is null || !PuedeEditar)
        {
            return;
        }

        var activar = !ProductoSeleccionado.Activo;

        await EjecutarAsync(async () =>
        {
            await _productos.CambiarEstadoAsync(ProductoSeleccionado.Id, activar).ConfigureAwait(true);

            _dialogos.Notificar(activar
                ? "Producto activado."
                : "Producto desactivado: dejará de aparecer en el punto de venta.");

            await BuscarAsync().ConfigureAwait(true);
        }, "No se pudo cambiar el estado del producto.");
    }

    [RelayCommand(CanExecute = nameof(PuedeOperarSobreSeleccion))]
    private async Task ImprimirEtiquetaAsync()
    {
        if (ProductoSeleccionado is null)
        {
            return;
        }

        var respuesta = await _dialogos.PedirTextoAsync(
            "Imprimir etiquetas",
            "¿Cuántas etiquetas desea imprimir?",
            "12").ConfigureAwait(true);

        if (respuesta is null)
        {
            return;
        }

        if (!int.TryParse(respuesta, out var copias) || copias is < 1 or > 500)
        {
            await _dialogos.InformarAsync("Cantidad no válida",
                "Indique un número de etiquetas entre 1 y 500.", esError: true).ConfigureAwait(true);
            return;
        }

        await EjecutarAsync(async () =>
        {
            var etiqueta = new EtiquetaProducto
            {
                Nombre = ProductoSeleccionado.Nombre,
                Codigo = ProductoSeleccionado.Codigo,
                CodigoBarras = ProductoSeleccionado.CodigoBarras,
                Precio = ProductoSeleccionado.PrecioConIva,
                UnidadAbreviatura = ProductoSeleccionado.UnidadAbreviatura,
                Copias = copias
            };

            var ruta = await _documentos.GenerarEtiquetasAsync(new[] { etiqueta }).ConfigureAwait(true);

            _archivos.AbrirConAplicacionPredeterminada(ruta);
            _dialogos.Notificar($"Se generaron {copias} etiquetas.");
        }, "No se pudieron generar las etiquetas.");
    }

    [RelayCommand]
    private void LimpiarFiltros()
    {
        TextoBusqueda = null;
        CategoriaSeleccionadaId = null;
        MarcaSeleccionadaId = null;
        EstadoSeleccionado = null;
        SoloActivos = true;
    }
}
