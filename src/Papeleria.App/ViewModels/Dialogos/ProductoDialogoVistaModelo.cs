using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Papeleria.App.Infrastructure;
using Papeleria.Business.Services;
using Papeleria.Business.Services.Catalogos;
using Papeleria.Domain.Entities;
using Papeleria.Domain.Exceptions;

namespace Papeleria.App.ViewModels.Dialogos;

/// <summary>Formulario de alta y edición de un producto.</summary>
public partial class ProductoDialogoVistaModelo : VistaModeloBase
{
    private readonly IServicioProductos _productos;
    private readonly IServicioCodigoBarras _codigoBarras;
    private readonly IServicioArchivos _archivos;
    private readonly IServicioDialogos _dialogos;
    private readonly Producto _producto;

    public ProductoDialogoVistaModelo(
        IServicioProductos productos,
        IServicioCodigoBarras codigoBarras,
        IServicioArchivos archivos,
        IServicioDialogos dialogos,
        Producto producto,
        bool esNuevo,
        IEnumerable<Categoria> categorias,
        IEnumerable<Marca> marcas,
        IEnumerable<UnidadMedida> unidades)
    {
        _productos = productos;
        _codigoBarras = codigoBarras;
        _archivos = archivos;
        _dialogos = dialogos;
        _producto = producto;

        EsNuevo = esNuevo;
        Titulo = esNuevo ? "Nuevo producto" : "Editar producto";

        Categorias = new ObservableCollection<Categoria>(categorias);
        Marcas = new ObservableCollection<Marca>(marcas);
        Unidades = new ObservableCollection<UnidadMedida>(unidades);

        CargarDesdeEntidad();
    }

    public bool EsNuevo { get; }

    public ObservableCollection<Categoria> Categorias { get; }

    public ObservableCollection<Marca> Marcas { get; }

    public ObservableCollection<UnidadMedida> Unidades { get; }

    [ObservableProperty] private string _codigo = string.Empty;
    [ObservableProperty] private string? _codigoBarrasTexto;
    [ObservableProperty] private string _nombre = string.Empty;
    [ObservableProperty] private string? _descripcion;
    [ObservableProperty] private int _categoriaId;
    [ObservableProperty] private int? _marcaId;
    [ObservableProperty] private int _unidadMedidaId;
    [ObservableProperty] private decimal _costo;
    [ObservableProperty] private decimal _precioVenta;
    [ObservableProperty] private decimal _porcentajeIva = 19m;
    [ObservableProperty] private decimal _stockActual;
    [ObservableProperty] private decimal _stockMinimo;
    [ObservableProperty] private decimal _stockMaximo;
    [ObservableProperty] private string? _ubicacion;
    [ObservableProperty] private string? _observaciones;
    [ObservableProperty] private string? _imagenPath;
    [ObservableProperty] private bool _activo = true;

    /// <summary>Imagen PNG del código de barras que se previsualiza en el formulario.</summary>
    [ObservableProperty]
    private byte[]? _imagenCodigoBarras;

    /// <summary>El stock solo es editable al crear: después se mueve mediante el kardex.</summary>
    public bool PuedeEditarStock => EsNuevo;

    public string TextoAyudaStock => EsNuevo
        ? "Cantidad con la que entra el producto. Queda registrada en el kardex como saldo inicial."
        : "Las existencias solo cambian mediante compras, ventas o ajustes de inventario.";

    /// <summary>Utilidad calculada en vivo mientras se escriben costo y precio.</summary>
    public decimal UtilidadUnitaria => PrecioVenta - Costo;

    public decimal MargenPorcentaje =>
        PrecioVenta <= 0 ? 0 : Math.Round((PrecioVenta - Costo) / PrecioVenta * 100m, 1);

    public decimal PrecioConIva =>
        Math.Round(PrecioVenta * (1 + PorcentajeIva / 100m), 2);

    partial void OnCostoChanged(decimal value) => NotificarCalculados();

    partial void OnPrecioVentaChanged(decimal value) => NotificarCalculados();

    partial void OnPorcentajeIvaChanged(decimal value) => NotificarCalculados();

    private void NotificarCalculados()
    {
        OnPropertyChanged(nameof(UtilidadUnitaria));
        OnPropertyChanged(nameof(MargenPorcentaje));
        OnPropertyChanged(nameof(PrecioConIva));
    }

    partial void OnCodigoBarrasTextoChanged(string? value) => RefrescarVistaPreviaCodigoBarras();

    private void CargarDesdeEntidad()
    {
        Codigo = _producto.Codigo;
        CodigoBarrasTexto = _producto.CodigoBarras;
        Nombre = _producto.Nombre;
        Descripcion = _producto.Descripcion;
        CategoriaId = _producto.CategoriaId;
        MarcaId = _producto.MarcaId;
        UnidadMedidaId = _producto.UnidadMedidaId;
        Costo = _producto.Costo;
        PrecioVenta = _producto.PrecioVenta;
        PorcentajeIva = _producto.PorcentajeIva;
        StockActual = _producto.StockActual;
        StockMinimo = _producto.StockMinimo;
        StockMaximo = _producto.StockMaximo;
        Ubicacion = _producto.Ubicacion;
        Observaciones = _producto.Observaciones;
        ImagenPath = _producto.ImagenPath;
        Activo = _producto.Activo;

        // Valores por defecto razonables para un alta rápida.
        if (CategoriaId == 0 && Categorias.Count > 0)
        {
            CategoriaId = Categorias[0].Id;
        }

        if (UnidadMedidaId == 0 && Unidades.Count > 0)
        {
            UnidadMedidaId = Unidades[0].Id;
        }

        RefrescarVistaPreviaCodigoBarras();
    }

    private void RefrescarVistaPreviaCodigoBarras()
    {
        if (string.IsNullOrWhiteSpace(CodigoBarrasTexto))
        {
            ImagenCodigoBarras = null;
            return;
        }

        try
        {
            ImagenCodigoBarras = _codigoBarras.GenerarPng(CodigoBarrasTexto,
                SimbologiaCodigoBarras.Automatica, 420, 110);
        }
        catch (NegocioException)
        {
            // Mientras el usuario escribe, el contenido puede no ser válido todavía.
            ImagenCodigoBarras = null;
        }
    }

    [RelayCommand]
    private async Task GenerarCodigoAsync() => await EjecutarAsync(async () =>
        Codigo = await _productos.SugerirCodigoAsync().ConfigureAwait(true));

    [RelayCommand]
    private async Task GenerarCodigoBarrasAsync() => await EjecutarAsync(async () =>
        CodigoBarrasTexto = await _productos.GenerarCodigoBarrasAsync().ConfigureAwait(true));

    [RelayCommand]
    private async Task SeleccionarImagenAsync()
    {
        var ruta = _archivos.SeleccionarArchivo(
            "Seleccionar imagen del producto",
            "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos los archivos|*.*");

        if (string.IsNullOrWhiteSpace(ruta))
        {
            return;
        }

        await EjecutarAsync(async () =>
            ImagenPath = await _archivos.GuardarImagenAsync(ruta, "producto").ConfigureAwait(true));
    }

    [RelayCommand]
    private void QuitarImagen() => ImagenPath = null;

    [RelayCommand]
    private async Task GuardarAsync()
    {
        await EjecutarAsync(async () =>
        {
            VolcarEnEntidad();

            if (EsNuevo)
            {
                await _productos.CrearAsync(_producto).ConfigureAwait(true);
            }
            else
            {
                await _productos.ActualizarAsync(_producto).ConfigureAwait(true);
            }

            _dialogos.Cerrar(true);
        }, "No se pudo guardar el producto.");
    }

    [RelayCommand]
    private void Cancelar() => _dialogos.Cerrar(false);

    private void VolcarEnEntidad()
    {
        _producto.Codigo = Codigo;
        _producto.CodigoBarras = CodigoBarrasTexto;
        _producto.Nombre = Nombre;
        _producto.Descripcion = Descripcion;
        _producto.CategoriaId = CategoriaId;
        _producto.MarcaId = MarcaId;
        _producto.UnidadMedidaId = UnidadMedidaId;
        _producto.Costo = Costo;
        _producto.PrecioVenta = PrecioVenta;
        _producto.PorcentajeIva = PorcentajeIva;
        _producto.StockMinimo = StockMinimo;
        _producto.StockMaximo = StockMaximo;
        _producto.Ubicacion = Ubicacion;
        _producto.Observaciones = Observaciones;
        _producto.ImagenPath = ImagenPath;
        _producto.Activo = Activo;

        // Al editar, el stock lo gobierna el kardex y no este formulario.
        if (EsNuevo)
        {
            _producto.StockActual = StockActual;
        }
    }
}
