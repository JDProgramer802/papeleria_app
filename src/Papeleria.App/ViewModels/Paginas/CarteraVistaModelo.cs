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

namespace Papeleria.App.ViewModels.Paginas;

/// <summary>Filtro rápido por antigüedad de la deuda.</summary>
public enum FiltroMora
{
    Todos = 0,
    Mas30 = 30,
    Mas60 = 60,
    Mas90 = 90
}

/// <summary>
/// Cuentas por cobrar: quién debe, cuánto y desde cuándo. Permite recibir abonos en
/// el mostrador y consultar el estado de cuenta de cada cliente.
/// </summary>
public partial class CarteraVistaModelo : PaginaVistaModelo
{
    private readonly IServicioCartera _cartera;
    private readonly IServicioDialogos _dialogos;
    private readonly IContextoSesion _sesion;

    public CarteraVistaModelo(
        IServicioCartera cartera,
        IServicioDialogos dialogos,
        IContextoSesion sesion)
    {
        _cartera = cartera;
        _dialogos = dialogos;
        _sesion = sesion;

        Titulo = "Cartera";
        Subtitulo = "Cuentas por cobrar de las ventas a crédito";

        Paginador.PaginaCambiada += (_, _) => _ = BuscarAsync();

        // Una venta fiada cambia la deuda al instante.
        WeakReferenceMessenger.Default.Register<CarteraVistaModelo, VentaRegistradaMensaje>(
            this, (destinatario, mensaje) => { _ = destinatario.BuscarAsync(); });
    }

    public override string Modulo => Modulos.Cartera;

    public Paginador Paginador { get; } = new();

    public ObservableCollection<SaldoClienteDto> Deudores { get; } = new();

    public ObservableCollection<FacturaCreditoDto> Facturas { get; } = new();

    public ObservableCollection<AbonoDto> Abonos { get; } = new();

    public IReadOnlyList<KeyValuePair<FiltroMora, string>> OpcionesMora { get; } =
        new List<KeyValuePair<FiltroMora, string>>
        {
            new(FiltroMora.Todos, "Toda la cartera"),
            new(FiltroMora.Mas30, "Más de 30 días"),
            new(FiltroMora.Mas60, "Más de 60 días"),
            new(FiltroMora.Mas90, "Más de 90 días")
        };

    [ObservableProperty] private string? _textoBusqueda;
    [ObservableProperty] private bool _soloConSaldo = true;
    [ObservableProperty] private FiltroMora _moraSeleccionada = FiltroMora.Todos;
    [ObservableProperty] private ResumenCarteraDto? _resumen;
    [ObservableProperty] private EstadoCuentaDto? _estadoCuenta;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccion))]
    private SaldoClienteDto? _deudorSeleccionado;

    public bool PuedeAbonar => _sesion.Puede(Modulos.Cartera, AccionPermiso.Crear);

    public bool PuedeAnularAbono => _sesion.EsAdministrador;

    public bool HaySeleccion => DeudorSeleccionado is not null;

    partial void OnDeudorSeleccionadoChanged(SaldoClienteDto? value)
    {
        AbonarCommand.NotifyCanExecuteChanged();
        _ = CargarEstadoCuentaAsync();
    }

    partial void OnTextoBusquedaChanged(string? value) => ReiniciarBusqueda();
    partial void OnSoloConSaldoChanged(bool value) => ReiniciarBusqueda();
    partial void OnMoraSeleccionadaChanged(FiltroMora value) => ReiniciarBusqueda();

    private void ReiniciarBusqueda()
    {
        Paginador.Reiniciar();
        _ = BuscarAsync();
    }

    public override Task CargarAsync() => BuscarAsync();

    private FiltroCartera ConstruirFiltro() => new()
    {
        Texto = TextoBusqueda,
        SoloConSaldo = SoloConSaldo,
        DiasMoraMinimos = MoraSeleccionada == FiltroMora.Todos ? null : (int)MoraSeleccionada,
        Pagina = Paginador.Pagina,
        TamanoPagina = Paginador.TamanoPagina
    };

    [RelayCommand]
    private Task BuscarAsync() => EjecutarAsync(async () =>
    {
        var filtro = ConstruirFiltro();

        var pagina = await _cartera.BuscarAsync(filtro).ConfigureAwait(true);

        Deudores.Clear();

        foreach (var deudor in pagina.Elementos)
        {
            Deudores.Add(deudor);
        }

        Paginador.Actualizar(pagina);

        // Las cifras se calculan sobre toda la cartera filtrada, no sobre la página.
        Resumen = await _cartera.ObtenerResumenAsync(filtro).ConfigureAwait(true);
    }, "No se pudo consultar la cartera.");

    private Task CargarEstadoCuentaAsync() => EjecutarAsync(async () =>
    {
        Facturas.Clear();
        Abonos.Clear();

        if (DeudorSeleccionado is null)
        {
            EstadoCuenta = null;
            return;
        }

        var estado = await _cartera
            .ObtenerEstadoCuentaAsync(DeudorSeleccionado.ClienteId)
            .ConfigureAwait(true);

        EstadoCuenta = estado;

        foreach (var factura in estado.Facturas)
        {
            Facturas.Add(factura);
        }

        foreach (var abono in estado.Abonos)
        {
            Abonos.Add(abono);
        }
    }, "No se pudo cargar el estado de cuenta.");

    private bool PuedeOperarSobreSeleccion() => DeudorSeleccionado is not null;

    [RelayCommand(CanExecute = nameof(PuedeOperarSobreSeleccion))]
    private async Task AbonarAsync()
    {
        if (DeudorSeleccionado is null || !PuedeAbonar)
        {
            return;
        }

        var cliente = DeudorSeleccionado;
        AbonoDialogoVistaModelo? dialogo = null;

        dialogo = new AbonoDialogoVistaModelo(
            _dialogos,
            async () =>
            {
                var abono = await _cartera.RegistrarAbonoAsync(new SolicitudAbono
                {
                    ClienteId = cliente.ClienteId,
                    Monto = dialogo!.Monto,
                    MetodoPago = dialogo.MetodoPago,
                    Observaciones = dialogo.Observaciones
                }).ConfigureAwait(true);

                _dialogos.Notificar(
                    $"Abono de {Formatos.Moneda(abono.Monto)} registrado a {abono.ClienteNombre}.");
            },
            cliente.Nombre,
            cliente.Saldo);

        if (await _dialogos.MostrarAsync(dialogo).ConfigureAwait(true) is not true)
        {
            return;
        }

        // El efectivo recibido cambia el arqueo del turno abierto.
        WeakReferenceMessenger.Default.Send(new CajaCambiadaMensaje(true));

        await RecargarConservandoSeleccionAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AnularAbonoAsync(AbonoDto? abono)
    {
        if (abono is null || abono.Anulado || !PuedeAnularAbono)
        {
            return;
        }

        var motivo = await _dialogos.PedirTextoAsync(
            "Anular abono",
            $"Se devolverá {Formatos.Moneda(abono.Monto)} a la deuda de {abono.ClienteNombre}. " +
            "Indique el motivo:").ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            await _cartera.AnularAbonoAsync(abono.Id, motivo).ConfigureAwait(true);

            _dialogos.Notificar("Abono anulado.");

            WeakReferenceMessenger.Default.Send(new CajaCambiadaMensaje(true));

            await RecargarConservandoSeleccionAsync().ConfigureAwait(true);
        }, "No se pudo anular el abono.");
    }

    /// <summary>Refresca la lista sin perder de vista al cliente que se estaba revisando.</summary>
    private async Task RecargarConservandoSeleccionAsync()
    {
        var clienteId = DeudorSeleccionado?.ClienteId;

        await BuscarAsync().ConfigureAwait(true);

        DeudorSeleccionado = clienteId is null
            ? null
            : Deudores.FirstOrDefault(d => d.ClienteId == clienteId);

        // Si ya quedó sin deuda desaparece de la lista, pero su cuenta sigue siendo útil.
        if (DeudorSeleccionado is null && clienteId is { } id)
        {
            EstadoCuenta = await _cartera.ObtenerEstadoCuentaAsync(id).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void LimpiarFiltros()
    {
        TextoBusqueda = null;
        MoraSeleccionada = FiltroMora.Todos;
        SoloConSaldo = true;
    }

    [RelayCommand]
    private Task ActualizarAsync() => BuscarAsync();
}
