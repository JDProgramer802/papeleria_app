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
using Papeleria.Domain.Enums;

namespace Papeleria.App.ViewModels.Paginas;

/// <summary>
/// Cotizaciones emitidas: consultarlas, imprimirlas y convertirlas en factura cuando
/// el cliente vuelve a aceptar. Se arman desde el punto de venta, con el mismo carrito
/// de siempre, para no tener que aprender otra pantalla.
/// </summary>
public partial class CotizacionesVistaModelo : PaginaVistaModelo
{
    private readonly IServicioCotizaciones _cotizaciones;
    private readonly IServicioCartera _cartera;
    private readonly IServicioCaja _caja;
    private readonly IServicioDocumentos _documentos;
    private readonly IServicioArchivos _archivos;
    private readonly IServicioDialogos _dialogos;
    private readonly IContextoSesion _sesion;

    public CotizacionesVistaModelo(
        IServicioCotizaciones cotizaciones,
        IServicioCartera cartera,
        IServicioCaja caja,
        IServicioDocumentos documentos,
        IServicioArchivos archivos,
        IServicioDialogos dialogos,
        IContextoSesion sesion)
    {
        _cotizaciones = cotizaciones;
        _cartera = cartera;
        _caja = caja;
        _documentos = documentos;
        _archivos = archivos;
        _dialogos = dialogos;
        _sesion = sesion;

        Titulo = "Cotizaciones";
        Subtitulo = "Precios en firme que se le pasaron a un cliente";

        Paginador.PaginaCambiada += (_, _) => { _ = BuscarAsync(); };

        // Si se factura una cotización desde otra pantalla, la lista se refresca sola.
        WeakReferenceMessenger.Default.Register<CotizacionesVistaModelo, VentaRegistradaMensaje>(
            this, static (destinatario, _) => destinatario.RefrescarEnSegundoPlano());
    }

    public override string Modulo => Modulos.Cotizaciones;

    public Paginador Paginador { get; } = new();

    public ObservableCollection<CotizacionResumenDto> Cotizaciones { get; } = new();

    public ObservableCollection<OpcionEnum<EstadoCotizacion>> Estados { get; } =
        new(Enumeraciones.Opciones<EstadoCotizacion>());

    [ObservableProperty] private string? _textoBusqueda;
    [ObservableProperty] private DateTime _desde = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime _hasta = DateTime.Today;
    [ObservableProperty] private EstadoCotizacion? _estadoSeleccionado;
    [ObservableProperty] private bool _soloVigentes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccion))]
    [NotifyCanExecuteChangedFor(nameof(ImprimirCommand))]
    [NotifyCanExecuteChangedFor(nameof(FacturarCommand))]
    [NotifyCanExecuteChangedFor(nameof(RechazarCommand))]
    private CotizacionResumenDto? _seleccionada;

    [ObservableProperty] private CotizacionDetalladaDto? _detalle;

    public bool HaySeleccion => Seleccionada is not null;

    public bool PuedeFacturar => _sesion.Puede(Modulos.Ventas, AccionPermiso.Crear);

    public bool PuedeRechazar => _sesion.Puede(Modulos.Cotizaciones, AccionPermiso.Editar);

    partial void OnTextoBusquedaChanged(string? value) => Paginador.Reiniciar();

    partial void OnSeleccionadaChanged(CotizacionResumenDto? value) => _ = CargarDetalleAsync();

    public override Task CargarAsync() => BuscarAsync();

    private void RefrescarEnSegundoPlano() => _ = BuscarAsync();

    [RelayCommand]
    private Task BuscarAsync() => EjecutarAsync(async () =>
    {
        var resultado = await _cotizaciones.BuscarAsync(new FiltroCotizaciones
        {
            Texto = TextoBusqueda,
            Desde = Desde,
            Hasta = Hasta,
            Estado = EstadoSeleccionado,
            SoloVigentes = SoloVigentes,
            Pagina = Paginador.Pagina,
            TamanoPagina = Paginador.TamanoPagina
        }).ConfigureAwait(true);

        Cotizaciones.Clear();

        foreach (var cotizacion in resultado.Elementos)
        {
            Cotizaciones.Add(cotizacion);
        }

        Paginador.Actualizar(resultado);

        Seleccionada = Cotizaciones.FirstOrDefault();
    }, "No se pudieron cargar las cotizaciones.");

    [RelayCommand]
    private void LimpiarFiltros()
    {
        TextoBusqueda = null;
        Desde = DateTime.Today.AddMonths(-1);
        Hasta = DateTime.Today;
        EstadoSeleccionado = null;
        SoloVigentes = false;

        Paginador.Reiniciar();
        _ = BuscarAsync();
    }

    private Task CargarDetalleAsync() => EjecutarAsync(async () =>
    {
        if (Seleccionada is null)
        {
            Detalle = null;
            return;
        }

        Detalle = await _cotizaciones.ObtenerDetalleAsync(Seleccionada.Id).ConfigureAwait(true);
    }, "No se pudo cargar el detalle de la cotización.");

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private Task ImprimirAsync() => EjecutarAsync(async () =>
    {
        var ruta = await _cotizaciones.GenerarDocumentoAsync(Seleccionada!.Id).ConfigureAwait(true);
        _archivos.AbrirConAplicacionPredeterminada(ruta);
    }, "No se pudo generar el documento de la cotización.");

    /// <summary>
    /// El cliente aceptó. Se cobra igual que en el mostrador —con el mismo diálogo de
    /// pago— y la venta sale con los precios que se le cotizaron.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task FacturarAsync()
    {
        if (Seleccionada is null || Detalle is null || !PuedeFacturar)
        {
            return;
        }

        if (!Seleccionada.SePuedeConvertir)
        {
            await _dialogos.InformarAsync(
                "No se puede facturar",
                Seleccionada.Estado == EstadoCotizacion.Aceptada
                    ? $"La cotización {Seleccionada.Numero} ya se facturó como {Seleccionada.NumeroFactura}."
                    : $"La cotización {Seleccionada.Numero} está marcada como rechazada.",
                esError: true).ConfigureAwait(true);

            return;
        }

        if (!await _caja.HayCajaAbiertaAsync().ConfigureAwait(true))
        {
            await _dialogos.InformarAsync(
                "La caja está cerrada",
                "Para facturar hay que tener un turno de caja abierto.",
                esError: true).ConfigureAwait(true);

            return;
        }

        if (Seleccionada.EstaVencida)
        {
            var seguir = await _dialogos.ConfirmarAsync(
                "La cotización está vencida",
                $"Los precios de {Seleccionada.Numero} valían hasta el " +
                $"{Formatos.Fecha(Seleccionada.FechaVence)}. Si factura ahora, se respetan " +
                "esos precios aunque hayan cambiado. ¿Continuar?",
                "Facturar igual").ConfigureAwait(true);

            if (!seguir)
            {
                return;
            }
        }

        var dialogoPago = new PagoDialogoVistaModelo(
            _dialogos, Detalle.Total, await ConsultarCreditoAsync().ConfigureAwait(true));

        if (await _dialogos.MostrarAsync(dialogoPago).ConfigureAwait(true) is not true)
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            var venta = await _cotizaciones.ConvertirEnVentaAsync(
                Seleccionada.Id,
                new SolicitudConversionCotizacion
                {
                    MetodoPago = dialogoPago.MetodoPago,
                    MontoRecibido = dialogoPago.MetodoPago is MetodoPago.Efectivo or MetodoPago.Mixto
                        ? dialogoPago.MontoRecibido
                        : Detalle.Total,
                    ReferenciaPago = dialogoPago.ReferenciaPago
                }).ConfigureAwait(true);

            WeakReferenceMessenger.Default.Send(new VentaRegistradaMensaje(venta.NumeroFactura));
            WeakReferenceMessenger.Default.Send(new CajaCambiadaMensaje(true));

            _dialogos.Notificar($"Se facturó como {venta.NumeroFactura}.");

            if (dialogoPago.ImprimirFactura)
            {
                var ruta = await _documentos.GenerarFacturaAsync(venta).ConfigureAwait(true);
                _archivos.AbrirConAplicacionPredeterminada(ruta);
            }

            await BuscarAsync().ConfigureAwait(true);
        }, "No se pudo facturar la cotización.");
    }

    /// <summary>Cupo del cliente, por si quiere que se la fíen.</summary>
    private async Task<CreditoCliente?> ConsultarCreditoAsync()
    {
        if (Detalle is null)
        {
            return null;
        }

        try
        {
            var saldo = await _cartera.ObtenerSaldoAsync(Detalle.ClienteId).ConfigureAwait(true);

            return new CreditoCliente(
                Detalle.ClienteNombre, saldo.LimiteCredito > 0, saldo.CupoDisponible);
        }
        catch
        {
            // Si no se puede consultar el cupo, el cobro sigue disponible por los demás medios.
            return new CreditoCliente(Detalle.ClienteNombre, false, 0);
        }
    }

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task RechazarAsync()
    {
        if (Seleccionada is null || !PuedeRechazar)
        {
            return;
        }

        var confirmado = await _dialogos.ConfirmarAsync(
            "Marcar como rechazada",
            $"La cotización {Seleccionada.Numero} quedará como no aceptada. " +
            "Sigue en el histórico, solo deja de aparecer entre las vigentes.",
            "Marcar").ConfigureAwait(true);

        if (!confirmado)
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            await _cotizaciones.RechazarAsync(Seleccionada.Id).ConfigureAwait(true);
            await BuscarAsync().ConfigureAwait(true);
        }, "No se pudo marcar la cotización.");
    }

    /// <summary>Atajo al punto de venta, que es donde se arma una cotización nueva.</summary>
    [RelayCommand]
    private static void IrAPuntoDeVenta() =>
        WeakReferenceMessenger.Default.Send(new NavegarMensaje(Modulos.Ventas));
}
