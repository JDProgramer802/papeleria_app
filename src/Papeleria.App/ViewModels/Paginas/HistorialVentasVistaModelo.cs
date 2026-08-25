using System.Collections.ObjectModel;
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

/// <summary>Rango de fechas predefinido del historial.</summary>
public enum RangoRapido
{
    Hoy = 0,
    Ayer = 1,
    Semana = 2,
    Mes = 3,
    Personalizado = 4
}

/// <summary>
/// Consulta de las facturas ya emitidas: ventas del día, de un periodo o de un
/// cliente concreto, con reimpresión del comprobante y anulación para el administrador.
/// </summary>
public partial class HistorialVentasVistaModelo : PaginaVistaModelo
{
    private readonly IServicioVentas _ventas;
    private readonly IServicioDevoluciones _devoluciones;
    private readonly IServicioClientes _clientes;
    private readonly IServicioDocumentos _documentos;
    private readonly IServicioReportes _reportes;
    private readonly IServicioExportacion _exportacion;
    private readonly IServicioArchivos _archivos;
    private readonly IServicioDialogos _dialogos;
    private readonly IContextoSesion _sesion;

    public HistorialVentasVistaModelo(
        IServicioVentas ventas,
        IServicioDevoluciones devoluciones,
        IServicioClientes clientes,
        IServicioDocumentos documentos,
        IServicioReportes reportes,
        IServicioExportacion exportacion,
        IServicioArchivos archivos,
        IServicioDialogos dialogos,
        IContextoSesion sesion)
    {
        _ventas = ventas;
        _devoluciones = devoluciones;
        _clientes = clientes;
        _documentos = documentos;
        _reportes = reportes;
        _exportacion = exportacion;
        _archivos = archivos;
        _dialogos = dialogos;
        _sesion = sesion;

        Titulo = "Historial de ventas";
        Subtitulo = "Facturas emitidas, con reimpresión y detalle de cada venta";

        MetodosPago = new List<KeyValuePair<MetodoPago?, string>>
        {
            new(null, "Todos los medios de pago")
        };

        foreach (var opcion in Enumeraciones.Opciones<MetodoPago>())
        {
            MetodosPago.Add(new KeyValuePair<MetodoPago?, string>(opcion.Valor, opcion.Descripcion));
        }

        Paginador.PaginaCambiada += (_, _) => _ = BuscarAsync();

        // Una venta nueva desde el POS debe aparecer aquí sin recargar a mano.
        WeakReferenceMessenger.Default.Register<HistorialVentasVistaModelo, VentaRegistradaMensaje>(
            this, (destinatario, mensaje) => { _ = destinatario.BuscarAsync(); });
    }

    public override string Modulo => Modulos.HistorialVentas;

    public Paginador Paginador { get; } = new();

    public ObservableCollection<VentaResumenDto> Ventas { get; } = new();

    public ObservableCollection<Cliente> Clientes { get; } = new();

    public List<KeyValuePair<MetodoPago?, string>> MetodosPago { get; }

    /// <summary>Evita que un rango rápido se interprete como periodo personalizado.</summary>
    private bool _ajustandoRango;

    [ObservableProperty] private string? _textoBusqueda;
    [ObservableProperty] private DateTime? _desde = DateTime.Today;
    [ObservableProperty] private DateTime? _hasta = DateTime.Today;
    [ObservableProperty] private int? _clienteId;
    [ObservableProperty] private MetodoPago? _metodoPago;
    [ObservableProperty] private bool _incluirAnuladas = true;
    [ObservableProperty] private ResumenVentasDto? _resumen;
    [ObservableProperty] private string _descripcionPeriodo = "Ventas de hoy";
    [ObservableProperty] private VentaDetalladaDto? _detalle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsRangoHoy))]
    [NotifyPropertyChangedFor(nameof(EsRangoAyer))]
    [NotifyPropertyChangedFor(nameof(EsRangoSemana))]
    [NotifyPropertyChangedFor(nameof(EsRangoMes))]
    private RangoRapido _rangoActivo = RangoRapido.Hoy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccion))]
    private VentaResumenDto? _ventaSeleccionada;

    public bool HaySeleccion => VentaSeleccionada is not null;

    // Marcan qué botón de rango está activo.
    public bool EsRangoHoy => RangoActivo == RangoRapido.Hoy;
    public bool EsRangoAyer => RangoActivo == RangoRapido.Ayer;
    public bool EsRangoSemana => RangoActivo == RangoRapido.Semana;
    public bool EsRangoMes => RangoActivo == RangoRapido.Mes;

    /// <summary>Anular mueve inventario y dinero, así que queda en manos del administrador.</summary>
    public bool PuedeAnular => _sesion.EsAdministrador;

    partial void OnVentaSeleccionadaChanged(VentaResumenDto? value) => _ = CargarDetalleAsync();

    partial void OnTextoBusquedaChanged(string? value) => ReiniciarBusqueda();
    partial void OnClienteIdChanged(int? value) => ReiniciarBusqueda();
    partial void OnMetodoPagoChanged(MetodoPago? value) => ReiniciarBusqueda();
    partial void OnIncluirAnuladasChanged(bool value) => ReiniciarBusqueda();

    partial void OnDesdeChanged(DateTime? value)
    {
        if (_ajustandoRango)
        {
            return;
        }

        RangoActivo = RangoRapido.Personalizado;
        ReiniciarBusqueda();
    }

    partial void OnHastaChanged(DateTime? value)
    {
        if (_ajustandoRango)
        {
            return;
        }

        RangoActivo = RangoRapido.Personalizado;
        ReiniciarBusqueda();
    }

    private void ReiniciarBusqueda()
    {
        Paginador.Reiniciar();
        _ = BuscarAsync();
    }

    public override async Task CargarAsync()
    {
        await CargarClientesAsync().ConfigureAwait(true);
        await BuscarAsync().ConfigureAwait(true);
    }

    private Task CargarClientesAsync() => EjecutarAsync(async () =>
    {
        var clientes = await _clientes.ListarActivosAsync().ConfigureAwait(true);

        Clientes.Clear();
        Clientes.Add(new Cliente { Id = 0, Nombre = "Todos los clientes" });

        foreach (var cliente in clientes)
        {
            Clientes.Add(cliente);
        }
    }, "No se pudo cargar la lista de clientes.");

    private FiltroVentas ConstruirFiltro() => new()
    {
        Texto = TextoBusqueda,
        ClienteId = ClienteId,
        MetodoPago = MetodoPago,
        Desde = Desde,
        Hasta = Hasta,
        IncluirAnuladas = IncluirAnuladas,
        Pagina = Paginador.Pagina,
        TamanoPagina = Paginador.TamanoPagina
    };

    [RelayCommand]
    private Task BuscarAsync() => EjecutarAsync(async () =>
    {
        var filtro = ConstruirFiltro();

        var resultado = await _ventas.BuscarAsync(filtro).ConfigureAwait(true);

        Ventas.Clear();

        foreach (var venta in resultado.Elementos)
        {
            Ventas.Add(venta);
        }

        Paginador.Actualizar(resultado);

        // El resumen se pide aparte porque totaliza todo el rango, no solo la página.
        Resumen = await _ventas.ObtenerResumenAsync(filtro).ConfigureAwait(true);

        ActualizarDescripcionPeriodo();

        // Si la venta seleccionada ya no aparece en el listado, se limpia el detalle.
        if (VentaSeleccionada is not null && Ventas.All(v => v.Id != VentaSeleccionada.Id))
        {
            VentaSeleccionada = null;
        }
    }, "No se pudo consultar el historial de ventas.");

    private void ActualizarDescripcionPeriodo() => DescripcionPeriodo = RangoActivo switch
    {
        RangoRapido.Hoy => "Ventas de hoy",
        RangoRapido.Ayer => "Ventas de ayer",
        RangoRapido.Semana => "Últimos 7 días",
        RangoRapido.Mes => "Mes en curso",
        _ when Desde is { } d && Hasta is { } h && d.Date == h.Date => $"Ventas del {Formatos.Fecha(d)}",
        _ when Desde is { } di && Hasta is { } hf => $"{Formatos.Fecha(di)} — {Formatos.Fecha(hf)}",
        _ => "Todas las ventas"
    };

    private Task CargarDetalleAsync() => EjecutarAsync(async () =>
    {
        Detalle = VentaSeleccionada is null
            ? null
            : await _ventas.ObtenerDetalleAsync(VentaSeleccionada.Id).ConfigureAwait(true);
    }, "No se pudo cargar el detalle de la factura.");

    // ── Rangos rápidos ──────────────────────────────────────────────────────

    [RelayCommand]
    private void VerHoy() => AplicarRango(RangoRapido.Hoy, DateTime.Today, DateTime.Today);

    [RelayCommand]
    private void VerAyer() =>
        AplicarRango(RangoRapido.Ayer, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(-1));

    [RelayCommand]
    private void VerSemana() => AplicarRango(RangoRapido.Semana, DateTime.Today.AddDays(-6), DateTime.Today);

    [RelayCommand]
    private void VerMes() =>
        AplicarRango(RangoRapido.Mes, new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today);

    /// <summary>
    /// Fija el rango silenciando los observadores de fecha, que si no marcarían
    /// el periodo como personalizado y dispararían dos búsquedas seguidas.
    /// </summary>
    private void AplicarRango(RangoRapido rango, DateTime desde, DateTime hasta)
    {
        _ajustandoRango = true;

        try
        {
            Desde = desde;
            Hasta = hasta;
        }
        finally
        {
            _ajustandoRango = false;
        }

        RangoActivo = rango;

        Paginador.Reiniciar();
        _ = BuscarAsync();
    }

    [RelayCommand]
    private void LimpiarFiltros()
    {
        TextoBusqueda = null;
        ClienteId = null;
        MetodoPago = null;
        IncluirAnuladas = true;
        VerHoy();
    }

    // ── Acciones sobre la factura ───────────────────────────────────────────

    [RelayCommand]
    private Task ReimprimirReciboAsync() => ImprimirAsync(FormatoFactura.Recibo80mm);

    [RelayCommand]
    private Task ImprimirCartaAsync() => ImprimirAsync(FormatoFactura.Carta);

    /// <summary>
    /// Imprime una factura concreta desde su fila del listado, sin tener que abrir
    /// primero el detalle. Lleva el desglose de productos, cantidades y totales.
    /// </summary>
    [RelayCommand]
    private Task ImprimirFacturaAsync(VentaResumenDto? venta) => EjecutarAsync(async () =>
    {
        if (venta is null)
        {
            return;
        }

        var detalle = await _ventas.ObtenerDetalleAsync(venta.Id).ConfigureAwait(true);

        if (detalle is null)
        {
            return;
        }

        var ruta = await _documentos
            .GenerarFacturaAsync(detalle, FormatoFactura.Recibo80mm)
            .ConfigureAwait(true);

        _archivos.AbrirConAplicacionPredeterminada(ruta);
    }, "No se pudo preparar la factura para imprimir.");

    /// <summary>
    /// Imprime el listado completo del periodo consultado: una fila por factura con
    /// sus totales, que es el resumen que se archiva o se entrega al contador.
    /// </summary>
    [RelayCommand]
    private Task ImprimirListadoAsync() => EjecutarAsync(async () =>
    {
        var reporte = await _reportes
            .GenerarAsync(ConstruirParametrosReporte())
            .ConfigureAwait(true);

        if (!reporte.TieneDatos)
        {
            await _dialogos.InformarAsync(
                "Nada que imprimir",
                "No hay ventas en el periodo consultado.").ConfigureAwait(true);

            return;
        }

        // Va a un archivo temporal y se abre directamente: al imprimir no se
        // quiere elegir dónde guardar, se quiere ver el papel.
        var ruta = Data.Storage.RutasAplicacion.RutaTemporal(".pdf");

        await _exportacion.ExportarAsync(reporte, FormatoExportacion.Pdf, ruta).ConfigureAwait(true);

        _archivos.AbrirConAplicacionPredeterminada(ruta);
    }, "No se pudo preparar el listado para imprimir.");

    /// <summary>
    /// Parámetros del informe del periodo. Los comparten imprimir y exportar para que
    /// el papel y el archivo nunca muestren cosas distintas.
    /// </summary>
    private ParametrosReporte ConstruirParametrosReporte() => new()
    {
        Tipo = TipoReporte.Ventas,
        Desde = Desde ?? DateTime.Today,
        Hasta = Hasta ?? DateTime.Today,
        ClienteId = ClienteId
    };

    private Task ImprimirAsync(FormatoFactura formato) => EjecutarAsync(async () =>
    {
        if (Detalle is null)
        {
            return;
        }

        var ruta = await _documentos.GenerarFacturaAsync(Detalle, formato).ConfigureAwait(true);

        _archivos.AbrirConAplicacionPredeterminada(ruta);
    }, "No se pudo generar el comprobante de la factura.");

    public bool PuedeDevolver => _sesion.Puede(Modulos.Ventas, AccionPermiso.Editar);

    /// <summary>
    /// Devuelve parte de la factura sin anularla. En temporada escolar devolver es
    /// diario, y anular la factura entera para rehacerla rompe el consecutivo.
    /// </summary>
    [RelayCommand]
    private async Task DevolverAsync()
    {
        if (VentaSeleccionada is null || !PuedeDevolver)
        {
            return;
        }

        if (VentaSeleccionada.EstaAnulada)
        {
            await _dialogos.InformarAsync(
                "Factura anulada",
                "La factura está anulada: su mercancía ya volvió completa al inventario.",
                esError: true).ConfigureAwait(true);

            return;
        }

        VentaDevolvibleDto devolvible;

        try
        {
            devolvible = await _devoluciones
                .PrepararAsync(VentaSeleccionada.Id)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _dialogos.InformarAsync("No se pudo abrir la devolución", ex.Message, esError: true)
                .ConfigureAwait(true);

            return;
        }

        if (!devolvible.HayAlgoQueDevolver)
        {
            await _dialogos.InformarAsync(
                "Nada por devolver",
                "De esta factura ya se devolvió todo lo que se podía.").ConfigureAwait(true);

            return;
        }

        DevolucionDialogoVistaModelo? dialogo = null;

        dialogo = new DevolucionDialogoVistaModelo(
            _dialogos,
            async () =>
            {
                var devolucion = await _devoluciones.RegistrarAsync(new SolicitudDevolucion
                {
                    VentaId = devolvible.VentaId,
                    Motivo = dialogo!.Motivo,
                    Lineas = dialogo.ObtenerLineas()
                }).ConfigureAwait(true);

                _dialogos.Notificar(
                    $"Devolución {devolucion.Numero} por {Formatos.Moneda(devolucion.Total)} registrada.");
            },
            devolvible);

        if (await _dialogos.MostrarAsync(dialogo).ConfigureAwait(true) is not true)
        {
            return;
        }

        // La devolución mueve inventario y caja: ambos módulos deben enterarse.
        WeakReferenceMessenger.Default.Send(new InventarioCambiadoMensaje());
        WeakReferenceMessenger.Default.Send(new CajaCambiadaMensaje(true));

        await BuscarAsync().ConfigureAwait(true);
        await CargarDetalleAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AnularVentaAsync()
    {
        if (VentaSeleccionada is null || !PuedeAnular)
        {
            return;
        }

        if (VentaSeleccionada.EstaAnulada)
        {
            await _dialogos.InformarAsync("Factura anulada",
                $"La factura {VentaSeleccionada.NumeroFactura} ya estaba anulada.").ConfigureAwait(true);
            return;
        }

        var motivo = await _dialogos.PedirTextoAsync(
            $"Anular la factura {VentaSeleccionada.NumeroFactura}",
            "Motivo de la anulación",
            multilinea: true).ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return;
        }

        var identificador = VentaSeleccionada.Id;
        var numero = VentaSeleccionada.NumeroFactura;

        await EjecutarAsync(async () =>
        {
            await _ventas.AnularAsync(identificador, motivo).ConfigureAwait(true);

            _dialogos.Notificar($"Factura {numero} anulada. La mercancía volvió al inventario.");

            WeakReferenceMessenger.Default.Send(new InventarioCambiadoMensaje());

            await BuscarAsync().ConfigureAwait(true);
        }, "No se pudo anular la factura.");
    }

    // ── Exportación ─────────────────────────────────────────────────────────

    [RelayCommand]
    private Task ExportarExcelAsync() => ExportarAsync(FormatoExportacion.Excel);

    [RelayCommand]
    private Task ExportarPdfAsync() => ExportarAsync(FormatoExportacion.Pdf);

    [RelayCommand]
    private Task ExportarCsvAsync() => ExportarAsync(FormatoExportacion.Csv);

    private Task ExportarAsync(FormatoExportacion formato) => EjecutarAsync(async () =>
    {
        var reporte = await _reportes.GenerarAsync(ConstruirParametrosReporte()).ConfigureAwait(true);

        var extension = _exportacion.ObtenerExtension(formato);
        var etiqueta = extension.TrimStart('.').ToUpperInvariant();

        var ruta = _archivos.SeleccionarDondeGuardar(
            "Exportar historial de ventas",
            $"Archivo {etiqueta}|*{extension}",
            _exportacion.SugerirNombreArchivo(reporte, formato));

        if (string.IsNullOrWhiteSpace(ruta))
        {
            return;
        }

        await _exportacion.ExportarAsync(reporte, formato, ruta).ConfigureAwait(true);

        _dialogos.Notificar("Historial exportado correctamente.");
        _archivos.AbrirConAplicacionPredeterminada(ruta);
    }, "No se pudo exportar el historial.");

    [RelayCommand]
    private Task ActualizarAsync() => BuscarAsync();
}
