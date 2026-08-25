using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Papeleria.App.Infrastructure;
using Papeleria.Business.Dtos;
using Papeleria.Business.Security;
using Papeleria.Business.Services;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Entities;

namespace Papeleria.App.ViewModels.Paginas;

/// <summary>Línea del formulario de compra, con los totales recalculados en vivo.</summary>
public partial class LineaCompraEditable : ObservableObject
{
    public required int ProductoId { get; init; }

    public required string Codigo { get; init; }

    public required string Nombre { get; init; }

    public required string UnidadAbreviatura { get; init; }

    public decimal StockActual { get; init; }

    /// <summary>Unidades de venta que trae la presentación con la que se compra.</summary>
    public decimal UnidadesPorPresentacion { get; init; } = 1;

    /// <summary>El producto se compra en cajas o paquetes y se vende suelto.</summary>
    public bool TienePresentacion => UnidadesPorPresentacion > 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnidadesQueEntran))]
    private bool _porPresentacion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnidadesQueEntran))]
    private decimal _cantidad = 1;

    [ObservableProperty] private decimal _costoUnitario;
    [ObservableProperty] private decimal _porcentajeDescuento;
    [ObservableProperty] private decimal _porcentajeIva;

    /// <summary>
    /// Lo que realmente entrará al inventario. Se muestra en la fila para que el
    /// encargado vea que dos cajas son veinticuatro unidades antes de guardar.
    /// </summary>
    public string UnidadesQueEntran => PorPresentacion && TienePresentacion
        ? $"{Cantidad * UnidadesPorPresentacion:N0} und"
        : $"{Cantidad:N0} und";

    public decimal Subtotal => Business.Common.Dinero.Redondear(Cantidad * CostoUnitario);

    public decimal ValorDescuento => Business.Common.Dinero.Porcentaje(Subtotal, PorcentajeDescuento);

    public decimal BaseGravable => Business.Common.Dinero.Redondear(Subtotal - ValorDescuento);

    public decimal ValorIva => Business.Common.Dinero.Porcentaje(BaseGravable, PorcentajeIva);

    public decimal Total => Business.Common.Dinero.Redondear(BaseGravable + ValorIva);

    partial void OnCantidadChanged(decimal value) => NotificarTotales();
    partial void OnCostoUnitarioChanged(decimal value) => NotificarTotales();
    partial void OnPorcentajeDescuentoChanged(decimal value) => NotificarTotales();
    partial void OnPorcentajeIvaChanged(decimal value) => NotificarTotales();

    private void NotificarTotales()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(ValorDescuento));
        OnPropertyChanged(nameof(BaseGravable));
        OnPropertyChanged(nameof(ValorIva));
        OnPropertyChanged(nameof(Total));
    }
}

/// <summary>
/// Módulo de compras: historial de documentos y formulario de registro.
/// Ambos conviven en la misma pantalla alternando <see cref="EnModoRegistro"/>.
/// </summary>
public partial class ComprasVistaModelo : PaginaVistaModelo, IRecibeParametro
{
    private readonly IServicioCompras _compras;
    private readonly IServicioProveedores _proveedores;
    private readonly IServicioProductos _productos;
    private readonly IServicioDocumentos _documentos;
    private readonly IServicioArchivos _archivos;
    private readonly IServicioDialogos _dialogos;
    private readonly IServicioConfiguracion _configuracion;
    private readonly IContextoSesion _sesion;

    public ComprasVistaModelo(
        IServicioCompras compras,
        IServicioProveedores proveedores,
        IServicioProductos productos,
        IServicioDocumentos documentos,
        IServicioArchivos archivos,
        IServicioDialogos dialogos,
        IServicioConfiguracion configuracion,
        IContextoSesion sesion)
    {
        _compras = compras;
        _proveedores = proveedores;
        _productos = productos;
        _documentos = documentos;
        _archivos = archivos;
        _dialogos = dialogos;
        _configuracion = configuracion;
        _sesion = sesion;

        Titulo = "Compras";
        Subtitulo = "Registro de compras a proveedores y actualización de existencias";

        Paginador.PaginaCambiada += (_, _) => _ = BuscarAsync();
        Lineas.CollectionChanged += AlCambiarLineas;
    }

    public override string Modulo => Modulos.Compras;

    public Paginador Paginador { get; } = new();

    public ObservableCollection<CompraResumenDto> Historial { get; } = new();

    public ObservableCollection<Proveedor> Proveedores { get; } = new();

    public ObservableCollection<ProductoPosDto> ResultadosBusqueda { get; } = new();

    public ObservableCollection<LineaCompraEditable> Lineas { get; } = new();

    // ── Estado del historial ────────────────────────────────────────────────

    [ObservableProperty] private string? _textoBusquedaHistorial;
    [ObservableProperty] private DateTime? _desde = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime? _hasta = DateTime.Today;
    [ObservableProperty] private CompraResumenDto? _compraSeleccionada;

    // ── Estado del formulario ───────────────────────────────────────────────

    [ObservableProperty] private bool _enModoRegistro;
    [ObservableProperty] private int _proveedorId;
    [ObservableProperty] private string? _numeroFacturaProveedor;
    [ObservableProperty] private DateTime _fechaCompra = DateTime.Today;
    [ObservableProperty] private string? _observaciones;
    [ObservableProperty] private string? _textoBusquedaProducto;
    [ObservableProperty] private LineaCompraEditable? _lineaSeleccionada;

    public bool PuedeRegistrar => _sesion.Puede(Modulos.Compras, AccionPermiso.Crear);

    public bool PuedeAnular => _sesion.Puede(Modulos.Compras, AccionPermiso.Eliminar);

    public bool HayCompraSeleccionada => CompraSeleccionada is not null;

    public decimal Subtotal => Business.Common.Dinero.Redondear(Lineas.Sum(l => l.Subtotal));

    public decimal TotalDescuento => Business.Common.Dinero.Redondear(Lineas.Sum(l => l.ValorDescuento));

    public decimal TotalIva => Business.Common.Dinero.Redondear(Lineas.Sum(l => l.ValorIva));

    public decimal Total => Business.Common.Dinero.Redondear(Subtotal - TotalDescuento + TotalIva);

    public int CantidadLineas => Lineas.Count;

    public bool HayLineas => Lineas.Count > 0;

    partial void OnCompraSeleccionadaChanged(CompraResumenDto? value) =>
        OnPropertyChanged(nameof(HayCompraSeleccionada));

    partial void OnTextoBusquedaProductoChanged(string? value) => _ = BuscarProductosAsync();

    private void AlCambiarLineas(object? remitente, NotifyCollectionChangedEventArgs argumentos)
    {
        // Cada línea nueva debe notificar sus cambios para que los totales del pie se refresquen.
        if (argumentos.NewItems is not null)
        {
            foreach (LineaCompraEditable linea in argumentos.NewItems)
            {
                linea.PropertyChanged += AlCambiarLinea;
            }
        }

        if (argumentos.OldItems is not null)
        {
            foreach (LineaCompraEditable linea in argumentos.OldItems)
            {
                linea.PropertyChanged -= AlCambiarLinea;
            }
        }

        NotificarTotales();
    }

    private void AlCambiarLinea(object? remitente, PropertyChangedEventArgs argumentos) => NotificarTotales();

    private void NotificarTotales()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(TotalDescuento));
        OnPropertyChanged(nameof(TotalIva));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(CantidadLineas));
        OnPropertyChanged(nameof(HayLineas));
        GuardarCompraCommand.NotifyCanExecuteChanged();
    }

    public override async Task CargarAsync()
    {
        await CargarProveedoresAsync().ConfigureAwait(true);
        await BuscarAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Entrada desde el catálogo de productos: abre el formulario de compra con el
    /// artículo ya puesto en la primera línea, para no tener que buscarlo de nuevo.
    /// </summary>
    public async Task RecibirParametroAsync(object parametro)
    {
        await CargarAsync().ConfigureAwait(true);

        if (parametro is not CompraDeProducto solicitud)
        {
            return;
        }

        if (!PuedeRegistrar)
        {
            await _dialogos.InformarAsync(
                "Sin permiso",
                "Su usuario no puede registrar compras.",
                esError: true).ConfigureAwait(true);

            return;
        }

        await NuevaCompraAsync().ConfigureAwait(true);

        // Si no hay proveedores, NuevaCompraAsync ya avisó y no abrió el formulario.
        if (!EnModoRegistro)
        {
            return;
        }

        var producto = await ObtenerProductoParaCompraAsync(solicitud).ConfigureAwait(true);

        if (producto is null)
        {
            MensajeError = $"No se pudo cargar «{solicitud.Nombre}». Búsquelo en el formulario.";
            return;
        }

        AgregarProducto(producto);
    }

    /// <summary>Recupera la ficha del producto que se quiere comprar.</summary>
    private async Task<ProductoPosDto?> ObtenerProductoParaCompraAsync(CompraDeProducto solicitud)
    {
        try
        {
            var porCodigo = await _productos
                .BuscarPorCodigoExactoAsync(solicitud.Codigo)
                .ConfigureAwait(true);

            if (porCodigo is not null)
            {
                return porCodigo;
            }

            // Si el código cambió entre pantallas, se busca por nombre y se confirma por id.
            var candidatos = await _productos
                .BuscarParaVentaAsync(solicitud.Nombre, 25)
                .ConfigureAwait(true);

            return candidatos.FirstOrDefault(p => p.Id == solicitud.ProductoId);
        }
        catch (Exception ex)
        {
            MensajeError = ex.Message;
            return null;
        }
    }

    private Task CargarProveedoresAsync() => EjecutarAsync(async () =>
    {
        var proveedores = await _proveedores.ListarActivosAsync().ConfigureAwait(true);

        Proveedores.Clear();

        foreach (var proveedor in proveedores)
        {
            Proveedores.Add(proveedor);
        }
    }, "No se pudieron cargar los proveedores.");

    // ── Historial ───────────────────────────────────────────────────────────

    [RelayCommand]
    private Task BuscarAsync() => EjecutarAsync(async () =>
    {
        var resultado = await _compras.BuscarAsync(new FiltroCompras
        {
            Texto = TextoBusquedaHistorial,
            Desde = Desde,
            Hasta = Hasta,
            Pagina = Paginador.Pagina,
            TamanoPagina = Paginador.TamanoPagina
        }).ConfigureAwait(true);

        Historial.Clear();

        foreach (var compra in resultado.Elementos)
        {
            Historial.Add(compra);
        }

        Paginador.Actualizar(resultado);
    }, "No se pudo consultar el historial de compras.");

    [RelayCommand]
    private async Task VerComprobanteAsync()
    {
        if (CompraSeleccionada is null)
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            var detalle = await _compras.ObtenerDetalleAsync(CompraSeleccionada.Id).ConfigureAwait(true);

            if (detalle is null)
            {
                return;
            }

            var ruta = await _documentos.GenerarComprobanteCompraAsync(detalle).ConfigureAwait(true);
            _archivos.AbrirConAplicacionPredeterminada(ruta);
        }, "No se pudo generar el comprobante de la compra.");
    }

    [RelayCommand]
    private async Task AnularCompraAsync()
    {
        if (CompraSeleccionada is null || !PuedeAnular)
        {
            return;
        }

        if (CompraSeleccionada.EstaAnulada)
        {
            await _dialogos.InformarAsync("Compra anulada",
                "Esta compra ya se encuentra anulada.").ConfigureAwait(true);
            return;
        }

        var motivo = await _dialogos.PedirTextoAsync(
            $"Anular la compra {CompraSeleccionada.Numero}",
            "Motivo de la anulación",
            multilinea: true).ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            await _compras.AnularAsync(CompraSeleccionada.Id, motivo).ConfigureAwait(true);

            _dialogos.Notificar($"Compra {CompraSeleccionada.Numero} anulada.");

            WeakReferenceMessenger.Default.Send(new CompraRegistradaMensaje(CompraSeleccionada.Numero));
            WeakReferenceMessenger.Default.Send(new InventarioCambiadoMensaje());

            await BuscarAsync().ConfigureAwait(true);
        }, "No se pudo anular la compra.");
    }

    // ── Formulario de registro ──────────────────────────────────────────────

    [RelayCommand]
    private async Task NuevaCompraAsync()
    {
        if (!PuedeRegistrar)
        {
            return;
        }

        if (Proveedores.Count == 0)
        {
            await CargarProveedoresAsync().ConfigureAwait(true);
        }

        if (Proveedores.Count == 0)
        {
            await _dialogos.InformarAsync(
                "Sin proveedores",
                "Registre al menos un proveedor antes de crear una compra.",
                esError: true).ConfigureAwait(true);

            return;
        }

        LimpiarFormulario();
        EnModoRegistro = true;
    }

    [RelayCommand]
    private async Task CancelarRegistroAsync()
    {
        if (Lineas.Count > 0)
        {
            var confirmado = await _dialogos.ConfirmarAsync(
                "Descartar compra",
                "Se perderán las líneas capturadas. ¿Desea salir del formulario?",
                "Descartar", esDestructivo: true).ConfigureAwait(true);

            if (!confirmado)
            {
                return;
            }
        }

        LimpiarFormulario();
        EnModoRegistro = false;
    }

    private void LimpiarFormulario()
    {
        Lineas.Clear();
        ResultadosBusqueda.Clear();
        ProveedorId = Proveedores.FirstOrDefault()?.Id ?? 0;
        NumeroFacturaProveedor = null;
        Observaciones = null;
        FechaCompra = DateTime.Today;
        TextoBusquedaProducto = null;
        MensajeError = null;
    }

    /// <summary>
    /// Unidades por presentación de cada producto buscado. El DTO del punto de venta
    /// no las trae, así que se consultan una sola vez por producto.
    /// </summary>
    private readonly Dictionary<int, decimal> _fichasPresentacion = new();

    private Task BuscarProductosAsync() => EjecutarAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(TextoBusquedaProducto) || TextoBusquedaProducto.Length < 2)
        {
            ResultadosBusqueda.Clear();
            return;
        }

        var resultados = await _productos
            .BuscarParaVentaAsync(TextoBusquedaProducto, 25)
            .ConfigureAwait(true);

        ResultadosBusqueda.Clear();

        foreach (var producto in resultados)
        {
            ResultadosBusqueda.Add(producto);

            if (!_fichasPresentacion.ContainsKey(producto.Id))
            {
                var ficha = await _productos.ObtenerAsync(producto.Id).ConfigureAwait(true);
                _fichasPresentacion[producto.Id] = ficha?.UnidadesPorPresentacion ?? 1m;
            }
        }
    }, "No se pudo buscar productos.");

    [RelayCommand]
    private void AgregarProducto(ProductoPosDto? producto)
    {
        if (producto is null)
        {
            return;
        }

        // Si el producto ya está en la compra, se acumula la cantidad.
        var existente = Lineas.FirstOrDefault(l => l.ProductoId == producto.Id);

        if (existente is not null)
        {
            existente.Cantidad += 1;
            LineaSeleccionada = existente;
            return;
        }

        var ficha = _fichasPresentacion.TryGetValue(producto.Id, out var unidades) ? unidades : 1m;

        var linea = new LineaCompraEditable
        {
            ProductoId = producto.Id,
            Codigo = producto.Codigo,
            Nombre = producto.Nombre,
            UnidadAbreviatura = producto.UnidadAbreviatura,
            StockActual = producto.StockActual,
            UnidadesPorPresentacion = ficha,
            // Si el producto se compra por caja, esa es la forma habitual de recibirlo.
            PorPresentacion = ficha > 1,
            Cantidad = 1,
            CostoUnitario = producto.Costo,
            PorcentajeIva = producto.PorcentajeIva
        };

        Lineas.Add(linea);
        LineaSeleccionada = linea;

        TextoBusquedaProducto = null;
        ResultadosBusqueda.Clear();
    }

    [RelayCommand]
    private void QuitarLinea(LineaCompraEditable? linea)
    {
        var objetivo = linea ?? LineaSeleccionada;

        if (objetivo is not null)
        {
            Lineas.Remove(objetivo);
        }
    }

    private bool PuedeGuardarCompra() => Lineas.Count > 0 && ProveedorId > 0 && !EstaCargando;

    [RelayCommand(CanExecute = nameof(PuedeGuardarCompra))]
    private async Task GuardarCompraAsync()
    {
        var confirmado = await _dialogos.ConfirmarAsync(
            "Registrar compra",
            $"Se registrarán {Lineas.Count} línea(s) por un total de " +
            $"{Business.Common.Formatos.Moneda(Total)}.\n\n" +
            "El inventario y el kardex se actualizarán automáticamente.",
            "Registrar").ConfigureAwait(true);

        if (!confirmado)
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            var solicitud = new SolicitudCompra
            {
                ProveedorId = ProveedorId,
                NumeroFacturaProveedor = NumeroFacturaProveedor,
                Fecha = FechaCompra,
                Observaciones = Observaciones,
                Lineas = Lineas.Select(l => new LineaCompra
                {
                    ProductoId = l.ProductoId,
                    Cantidad = l.Cantidad,
                    PorPresentacion = l.PorPresentacion,
                    CostoUnitario = l.CostoUnitario,
                    PorcentajeDescuento = l.PorcentajeDescuento,
                    PorcentajeIva = l.PorcentajeIva,
                    DescripcionProducto = l.Nombre
                }).ToList()
            };

            var compra = await _compras.RegistrarAsync(solicitud).ConfigureAwait(true);

            _dialogos.Notificar($"Compra {compra.Numero} registrada por " +
                                $"{Business.Common.Formatos.Moneda(compra.Total)}.");

            WeakReferenceMessenger.Default.Send(new CompraRegistradaMensaje(compra.Numero));
            WeakReferenceMessenger.Default.Send(new InventarioCambiadoMensaje());

            LimpiarFormulario();
            EnModoRegistro = false;

            await BuscarAsync().ConfigureAwait(true);
        }, "No se pudo registrar la compra.");
    }

    [RelayCommand]
    private void AplicarIvaGeneral()
    {
        // Aplica a todas las líneas el IVA configurado por defecto para la empresa.
        var iva = _configuracion.ObtenerEmpresa().IvaPorDefecto;

        foreach (var linea in Lineas)
        {
            linea.PorcentajeIva = iva;
        }
    }
}
