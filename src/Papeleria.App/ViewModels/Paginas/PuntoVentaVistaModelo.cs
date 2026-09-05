using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Papeleria.App.Infrastructure;
using Papeleria.App.ViewModels.Dialogos;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;
using Papeleria.Business.Security;
using Papeleria.Business.Services;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Entities;
using Papeleria.Domain.Enums;

namespace Papeleria.App.ViewModels.Paginas;

/// <summary>Línea del carrito del punto de venta.</summary>
public partial class LineaCarrito : ObservableObject
{
    public required int ProductoId { get; init; }

    public required string Codigo { get; init; }

    public required string Nombre { get; init; }

    public required string UnidadAbreviatura { get; init; }

    public decimal CostoUnitario { get; init; }

    /// <summary>Existencias disponibles al agregar la línea; impide sobrepasarlas.</summary>
    public decimal StockDisponible { get; init; }

    /// <summary>
    /// La línea descuenta inventario. Una fotocopia o un anillado no: se cobran las
    /// veces que haga falta sin que existan «unidades» que se acaben.
    /// </summary>
    public bool ControlaExistencias { get; init; } = true;

    public string DisponibilidadTexto => ControlaExistencias
        ? $"disponible {Formatos.Cantidad(StockDisponible)}"
        : "servicio";

    [ObservableProperty] private decimal _cantidad = 1;
    [ObservableProperty] private decimal _precioUnitario;
    [ObservableProperty] private decimal _porcentajeDescuento;
    [ObservableProperty] private decimal _porcentajeIva;

    public decimal Subtotal => Dinero.Redondear(Cantidad * PrecioUnitario);

    public decimal ValorDescuento => Dinero.Porcentaje(Subtotal, PorcentajeDescuento);

    public decimal BaseGravable => Dinero.Redondear(Subtotal - ValorDescuento);

    public decimal ValorIva => Dinero.Porcentaje(BaseGravable, PorcentajeIva);

    public decimal Total => Dinero.Redondear(BaseGravable + ValorIva);

    public bool ExcedeStock => ControlaExistencias && Cantidad > StockDisponible;

    /// <summary>Cabe una unidad más. Un servicio siempre admite otra.</summary>
    public bool AdmiteUnaMas => !ControlaExistencias || Cantidad + 1 <= StockDisponible;

    partial void OnCantidadChanged(decimal value) => NotificarTotales();
    partial void OnPrecioUnitarioChanged(decimal value) => NotificarTotales();
    partial void OnPorcentajeDescuentoChanged(decimal value) => NotificarTotales();
    partial void OnPorcentajeIvaChanged(decimal value) => NotificarTotales();

    private void NotificarTotales()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(ValorDescuento));
        OnPropertyChanged(nameof(BaseGravable));
        OnPropertyChanged(nameof(ValorIva));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(ExcedeStock));
    }
}

/// <summary>
/// Punto de venta. Está pensado para trabajar con lector de código de barras:
/// el cuadro de búsqueda recibe el código y al pulsar Enter agrega el producto.
/// </summary>
public partial class PuntoVentaVistaModelo : PaginaVistaModelo
{
    private readonly IServicioVentas _ventas;
    private readonly IServicioProductos _productos;
    private readonly IServicioClientes _clientes;
    private readonly IServicioCartera _cartera;
    private readonly IServicioCaja _caja;
    private readonly IServicioDocumentos _documentos;
    private readonly IServicioArchivos _archivos;
    private readonly IServicioDialogos _dialogos;
    private readonly IContextoSesion _sesion;

    private CancellationTokenSource? _cancelacionBusqueda;

    public PuntoVentaVistaModelo(
        IServicioVentas ventas,
        IServicioProductos productos,
        IServicioClientes clientes,
        IServicioCartera cartera,
        IServicioCaja caja,
        IServicioDocumentos documentos,
        IServicioArchivos archivos,
        IServicioDialogos dialogos,
        IContextoSesion sesion)
    {
        _ventas = ventas;
        _productos = productos;
        _clientes = clientes;
        _cartera = cartera;
        _caja = caja;
        _documentos = documentos;
        _archivos = archivos;
        _dialogos = dialogos;
        _sesion = sesion;

        Titulo = "Punto de venta";
        Subtitulo = "Facturación rápida con lector de código de barras";

        Carrito.CollectionChanged += AlCambiarCarrito;

        WeakReferenceMessenger.Default.Register<PuntoVentaVistaModelo, CajaCambiadaMensaje>(
            this, (destinatario, mensaje) => { _ = destinatario.ComprobarCajaAsync(); });
    }

    public override string Modulo => Modulos.Ventas;

    public ObservableCollection<LineaCarrito> Carrito { get; } = new();

    public ObservableCollection<ProductoPosDto> Resultados { get; } = new();

    public ObservableCollection<Cliente> Clientes { get; } = new();

    [ObservableProperty] private string? _textoBusqueda;
    [ObservableProperty] private int _clienteId;
    [ObservableProperty] private LineaCarrito? _lineaSeleccionada;
    [ObservableProperty] private bool _cajaAbierta;
    [ObservableProperty] private string? _observaciones;

    public bool PuedeVender => _sesion.Puede(Modulos.Ventas, AccionPermiso.Crear);

    public bool HayLineas => Carrito.Count > 0;

    public int CantidadArticulos => (int)Carrito.Sum(l => l.Cantidad);

    public decimal Subtotal => Dinero.Redondear(Carrito.Sum(l => l.Subtotal));

    public decimal TotalDescuento => Dinero.Redondear(Carrito.Sum(l => l.ValorDescuento));

    public decimal TotalIva => Dinero.Redondear(Carrito.Sum(l => l.ValorIva));

    public decimal Total => Dinero.Redondear(Subtotal - TotalDescuento + TotalIva);

    public decimal CostoTotal => Dinero.Redondear(Carrito.Sum(l => l.Cantidad * l.CostoUnitario));

    partial void OnTextoBusquedaChanged(string? value) => _ = BuscarConRetrasoAsync();

    private void AlCambiarCarrito(object? remitente, NotifyCollectionChangedEventArgs argumentos)
    {
        if (argumentos.NewItems is not null)
        {
            foreach (LineaCarrito linea in argumentos.NewItems)
            {
                linea.PropertyChanged += AlCambiarLinea;
            }
        }

        if (argumentos.OldItems is not null)
        {
            foreach (LineaCarrito linea in argumentos.OldItems)
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
        OnPropertyChanged(nameof(CostoTotal));
        OnPropertyChanged(nameof(HayLineas));
        OnPropertyChanged(nameof(CantidadArticulos));
        CobrarCommand.NotifyCanExecuteChanged();
    }

    public override async Task CargarAsync()
    {
        await CargarClientesAsync().ConfigureAwait(true);
        await ComprobarCajaAsync().ConfigureAwait(true);
    }

    public bool PuedeCrearClientes => _sesion.Puede(Modulos.Clientes, AccionPermiso.Crear);

    /// <summary>
    /// Cupo del cliente de la venta, para poder ofrecer el pago a crédito en el cobro
    /// y avisar en el momento si no le alcanza, en lugar de fallar al guardar.
    /// </summary>
    private async Task<CreditoCliente?> ConsultarCreditoAsync()
    {
        var cliente = Clientes.FirstOrDefault(c => c.Id == ClienteId);

        if (cliente is null)
        {
            return null;
        }

        try
        {
            var saldo = await _cartera.ObtenerSaldoAsync(cliente.Id).ConfigureAwait(true);

            // Al consumidor final no se le fía: no hay a quién cobrarle después.
            var admite = !cliente.EsProtegido && cliente.LimiteCredito > 0;

            return new CreditoCliente(cliente.Nombre, admite, saldo.CupoDisponible);
        }
        catch (Exception)
        {
            // Si la consulta falla, el cobro sigue disponible por los demás medios.
            return new CreditoCliente(cliente.Nombre, false, 0);
        }
    }

    /// <summary>
    /// Da de alta un cliente sin salir de la venta. En el mostrador el cliente aparece
    /// cuando ya se está facturando, y obligar a cambiar de módulo corta el cobro.
    /// </summary>
    [RelayCommand]
    private async Task NuevoClienteAsync()
    {
        if (!PuedeCrearClientes)
        {
            await _dialogos.InformarAsync(
                "Sin permiso",
                "Su usuario no puede crear clientes.",
                esError: true).ConfigureAwait(true);

            return;
        }

        Cliente? creado = null;
        ClienteDialogoVistaModelo? dialogo = null;

        dialogo = new ClienteDialogoVistaModelo(
            _dialogos,
            async () =>
            {
                creado = await _clientes.CrearAsync(new Cliente
                {
                    Nombre = dialogo!.Nombre,
                    TipoDocumento = dialogo.TipoDocumento,
                    NumeroDocumento = dialogo.NumeroDocumento,
                    Telefono = dialogo.Telefono,
                    Correo = dialogo.Correo,
                    Direccion = dialogo.Direccion,
                    Ciudad = dialogo.Ciudad,
                    Observaciones = dialogo.Observaciones,
                    LimiteCredito = dialogo.LimiteCredito,
                    Activo = true
                }).ConfigureAwait(true);
            },
            "Nuevo cliente");

        if (await _dialogos.MostrarAsync(dialogo).ConfigureAwait(true) is not true || creado is null)
        {
            return;
        }

        // Queda seleccionado para poder seguir cobrando de inmediato.
        Clientes.Add(creado);
        ClienteId = creado.Id;

        _dialogos.Notificar($"Cliente «{creado.Nombre}» creado y seleccionado.");
    }

    private Task CargarClientesAsync() => EjecutarAsync(async () =>
    {
        var clientes = await _clientes.ListarActivosAsync().ConfigureAwait(true);

        Clientes.Clear();

        foreach (var cliente in clientes)
        {
            Clientes.Add(cliente);
        }

        if (ClienteId == 0)
        {
            var consumidorFinal = await _clientes.ObtenerConsumidorFinalAsync().ConfigureAwait(true);
            ClienteId = consumidorFinal.Id;
        }
    }, "No se pudieron cargar los clientes.");

    private async Task ComprobarCajaAsync()
    {
        try
        {
            CajaAbierta = await _caja.HayCajaAbiertaAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "No se pudo comprobar el estado de la caja");
            CajaAbierta = false;
        }

        CobrarCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Búsqueda incremental con pequeño retardo: evita consultar en cada tecla y
    /// permite que un lector de códigos escriba la cadena completa antes de buscar.
    /// </summary>
    private async Task BuscarConRetrasoAsync()
    {
        _cancelacionBusqueda?.Cancel();
        _cancelacionBusqueda = new CancellationTokenSource();
        var token = _cancelacionBusqueda.Token;

        try
        {
            await Task.Delay(220, token).ConfigureAwait(true);

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(TextoBusqueda))
            {
                Resultados.Clear();
                return;
            }

            var encontrados = await _productos
                .BuscarParaVentaAsync(TextoBusqueda, 30)
                .ConfigureAwait(true);

            Resultados.Clear();

            foreach (var producto in encontrados)
            {
                Resultados.Add(producto);
            }
        }
        catch (TaskCanceledException)
        {
            // Búsqueda reemplazada por una más reciente.
        }
    }

    /// <summary>
    /// Se invoca al pulsar Enter en el cuadro de búsqueda: es el flujo del lector
    /// de código de barras. Si hay coincidencia exacta, agrega directamente.
    /// </summary>
    [RelayCommand]
    private async Task ProcesarCodigoAsync()
    {
        if (string.IsNullOrWhiteSpace(TextoBusqueda))
        {
            return;
        }

        var codigo = TextoBusqueda.Trim();

        await EjecutarAsync(async () =>
        {
            var producto = await _productos.BuscarPorCodigoExactoAsync(codigo).ConfigureAwait(true);

            if (producto is not null)
            {
                AgregarProducto(producto);
                return;
            }

            // Sin coincidencia exacta: si la búsqueda dejó un único resultado, se usa ese.
            if (Resultados.Count == 1)
            {
                AgregarProducto(Resultados[0]);
                return;
            }

            if (Resultados.Count == 0)
            {
                MensajeError = $"No se encontró ningún producto con «{codigo}».";
            }
        }, "No se pudo buscar el producto.");
    }

    [RelayCommand]
    private void AgregarProducto(ProductoPosDto? producto)
    {
        if (producto is null)
        {
            return;
        }

        MensajeError = null;

        if (!producto.HayExistencias)
        {
            MensajeError = $"«{producto.Nombre}» está agotado y no puede venderse.";
            return;
        }

        var existente = Carrito.FirstOrDefault(l => l.ProductoId == producto.Id);

        if (existente is not null)
        {
            if (!existente.AdmiteUnaMas)
            {
                MensajeError = $"Solo hay {Formatos.Cantidad(existente.StockDisponible)} " +
                               $"unidades disponibles de «{producto.Nombre}».";
                return;
            }

            existente.Cantidad += 1;
            LineaSeleccionada = existente;
        }
        else
        {
            var linea = new LineaCarrito
            {
                ProductoId = producto.Id,
                Codigo = producto.Codigo,
                Nombre = producto.Nombre,
                UnidadAbreviatura = producto.UnidadAbreviatura,
                CostoUnitario = producto.Costo,
                StockDisponible = producto.StockActual,
                ControlaExistencias = !producto.EsServicio,
                Cantidad = 1,
                PrecioUnitario = producto.PrecioVenta,
                PorcentajeIva = producto.PorcentajeIva
            };

            Carrito.Add(linea);
            LineaSeleccionada = linea;
        }

        TextoBusqueda = null;
        Resultados.Clear();
    }

    [RelayCommand]
    private void AumentarCantidad(LineaCarrito? linea)
    {
        var objetivo = linea ?? LineaSeleccionada;

        if (objetivo is null)
        {
            return;
        }

        if (!objetivo.AdmiteUnaMas)
        {
            MensajeError = $"Solo hay {Formatos.Cantidad(objetivo.StockDisponible)} " +
                           $"unidades disponibles de «{objetivo.Nombre}».";
            return;
        }

        objetivo.Cantidad += 1;
    }

    [RelayCommand]
    private void DisminuirCantidad(LineaCarrito? linea)
    {
        var objetivo = linea ?? LineaSeleccionada;

        if (objetivo is null)
        {
            return;
        }

        if (objetivo.Cantidad <= 1)
        {
            Carrito.Remove(objetivo);
            return;
        }

        objetivo.Cantidad -= 1;
    }

    [RelayCommand]
    private void QuitarLinea(LineaCarrito? linea)
    {
        var objetivo = linea ?? LineaSeleccionada;

        if (objetivo is not null)
        {
            Carrito.Remove(objetivo);
        }
    }

    [RelayCommand]
    private async Task VaciarCarritoAsync()
    {
        if (Carrito.Count == 0)
        {
            return;
        }

        var confirmado = await _dialogos.ConfirmarAsync(
            "Vaciar carrito",
            "Se quitarán todos los productos de la venta en curso. ¿Continuar?",
            "Vaciar", esDestructivo: true).ConfigureAwait(true);

        if (confirmado)
        {
            Carrito.Clear();
            MensajeError = null;
        }
    }

    private bool PuedeCobrar() => Carrito.Count > 0 && CajaAbierta && PuedeVender && !EstaCargando;

    [RelayCommand(CanExecute = nameof(PuedeCobrar))]
    private async Task CobrarAsync()
    {
        var excedidas = Carrito.Where(l => l.ExcedeStock).ToList();

        if (excedidas.Count > 0)
        {
            MensajeError = "Hay líneas que superan las existencias disponibles: " +
                           string.Join(", ", excedidas.Select(l => l.Nombre));
            return;
        }

        var dialogoPago = new PagoDialogoVistaModelo(
            _dialogos, Total, await ConsultarCreditoAsync().ConfigureAwait(true));

        if (await _dialogos.MostrarAsync(dialogoPago).ConfigureAwait(true) is not true)
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            var solicitud = new SolicitudVenta
            {
                ClienteId = ClienteId,
                MetodoPago = dialogoPago.MetodoPago,
                MontoRecibido = dialogoPago.MetodoPago is MetodoPago.Efectivo or MetodoPago.Mixto
                    ? dialogoPago.MontoRecibido
                    : Total,
                ReferenciaPago = dialogoPago.ReferenciaPago,
                Observaciones = Observaciones,
                Lineas = Carrito.Select(l => new LineaVenta
                {
                    ProductoId = l.ProductoId,
                    Cantidad = l.Cantidad,
                    PrecioUnitario = l.PrecioUnitario,
                    CostoUnitario = l.CostoUnitario,
                    PorcentajeDescuento = l.PorcentajeDescuento,
                    PorcentajeIva = l.PorcentajeIva,
                    DescripcionProducto = l.Nombre
                }).ToList()
            };

            var venta = await _ventas.RegistrarAsync(solicitud).ConfigureAwait(true);

            Carrito.Clear();
            Observaciones = null;
            MensajeError = null;

            WeakReferenceMessenger.Default.Send(new VentaRegistradaMensaje(venta.NumeroFactura));
            WeakReferenceMessenger.Default.Send(new InventarioCambiadoMensaje());

            if (dialogoPago.ImprimirFactura)
            {
                await ImprimirFacturaAsync(venta).ConfigureAwait(true);
            }
        }, "No se pudo completar la venta.");
    }

    private async Task ImprimirFacturaAsync(VentaDetalladaDto venta)
    {
        try
        {
            var ruta = await _documentos
                .GenerarFacturaAsync(venta, FormatoFactura.Recibo80mm)
                .ConfigureAwait(true);

            _archivos.AbrirConAplicacionPredeterminada(ruta);
        }
        catch (Exception ex)
        {
            // La venta ya quedó registrada: un fallo al imprimir no debe revertirla.
            Serilog.Log.Error(ex, "No se pudo imprimir la factura {Numero}", venta.NumeroFactura);

            await _dialogos.InformarAsync(
                "Factura registrada",
                $"La venta {venta.NumeroFactura} se guardó correctamente, pero no se pudo abrir " +
                "el comprobante para imprimir. Puede reimprimirla desde el historial de ventas.",
                esError: true).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void IrACaja() => WeakReferenceMessenger.Default.Send(new NavegarMensaje(Modulos.Caja));
}
