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

namespace Papeleria.App.ViewModels.Paginas;

/// <summary>Apertura, movimientos, arqueo y cierre de caja, con su historial de turnos.</summary>
public partial class CajaVistaModelo : PaginaVistaModelo
{
    private readonly IServicioCaja _caja;
    private readonly IServicioDocumentos _documentos;
    private readonly IServicioArchivos _archivos;
    private readonly IServicioDialogos _dialogos;
    private readonly IContextoSesion _sesion;

    public CajaVistaModelo(
        IServicioCaja caja,
        IServicioDocumentos documentos,
        IServicioArchivos archivos,
        IServicioDialogos dialogos,
        IContextoSesion sesion)
    {
        _caja = caja;
        _documentos = documentos;
        _archivos = archivos;
        _dialogos = dialogos;
        _sesion = sesion;

        Titulo = "Caja";
        Subtitulo = "Turnos de caja, ingresos, egresos y arqueo";
    }

    public override string Modulo => Modulos.Caja;

    public ObservableCollection<CajaSesionDto> Historial { get; } = new();

    public ObservableCollection<MovimientoCajaDto> Movimientos { get; } = new();

    [ObservableProperty] private CajaSesion? _sesionAbierta;
    [ObservableProperty] private ArqueoCajaDto? _arqueo;
    [ObservableProperty] private CajaSesionDto? _sesionSeleccionada;
    [ObservableProperty] private DateTime? _desde = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime? _hasta = DateTime.Today;

    public bool HayCajaAbierta => SesionAbierta is not null;

    public bool PuedeAbrir => _sesion.Puede(Modulos.Caja, AccionPermiso.Crear) && !HayCajaAbierta;

    public bool PuedeOperar => _sesion.Puede(Modulos.Caja, AccionPermiso.Editar) && HayCajaAbierta;

    public bool HaySesionSeleccionada => SesionSeleccionada is not null;

    partial void OnSesionAbiertaChanged(CajaSesion? value)
    {
        OnPropertyChanged(nameof(HayCajaAbierta));
        OnPropertyChanged(nameof(PuedeAbrir));
        OnPropertyChanged(nameof(PuedeOperar));
        AbrirCajaCommand.NotifyCanExecuteChanged();
        RegistrarIngresoCommand.NotifyCanExecuteChanged();
        RegistrarEgresoCommand.NotifyCanExecuteChanged();
        CerrarCajaCommand.NotifyCanExecuteChanged();
    }

    partial void OnSesionSeleccionadaChanged(CajaSesionDto? value)
    {
        OnPropertyChanged(nameof(HaySesionSeleccionada));
        _ = CargarMovimientosAsync();
    }

    partial void OnDesdeChanged(DateTime? value) => _ = CargarHistorialAsync();

    partial void OnHastaChanged(DateTime? value) => _ = CargarHistorialAsync();

    public override async Task CargarAsync()
    {
        await CargarEstadoAsync().ConfigureAwait(true);
        await CargarHistorialAsync().ConfigureAwait(true);
    }

    private Task CargarEstadoAsync() => EjecutarAsync(async () =>
    {
        SesionAbierta = await _caja.ObtenerSesionAbiertaAsync().ConfigureAwait(true);

        Arqueo = SesionAbierta is null
            ? null
            : await _caja.CalcularArqueoAsync(SesionAbierta.Id).ConfigureAwait(true);
    }, "No se pudo consultar el estado de la caja.");

    private Task CargarHistorialAsync() => EjecutarAsync(async () =>
    {
        var sesiones = await _caja.ListarSesionesAsync(Desde, Hasta).ConfigureAwait(true);

        Historial.Clear();

        foreach (var sesion in sesiones)
        {
            Historial.Add(sesion);
        }
    }, "No se pudo cargar el historial de caja.");

    private Task CargarMovimientosAsync() => EjecutarAsync(async () =>
    {
        Movimientos.Clear();

        if (SesionSeleccionada is null)
        {
            return;
        }

        var movimientos = await _caja
            .ObtenerMovimientosAsync(SesionSeleccionada.Id)
            .ConfigureAwait(true);

        foreach (var movimiento in movimientos)
        {
            Movimientos.Add(movimiento);
        }
    }, "No se pudieron cargar los movimientos de la sesión.");

    [RelayCommand(CanExecute = nameof(PuedeAbrir))]
    private async Task AbrirCajaAsync()
    {
        var respuesta = await _dialogos.PedirTextoAsync(
            "Abrir caja",
            "Base inicial en efectivo",
            "0").ConfigureAwait(true);

        if (respuesta is null)
        {
            return;
        }

        if (!decimal.TryParse(respuesta, System.Globalization.NumberStyles.Any,
                Formatos.Cultura, out var monto) || monto < 0)
        {
            await _dialogos.InformarAsync("Monto no válido",
                "Escriba un importe numérico igual o mayor que cero.", esError: true).ConfigureAwait(true);
            return;
        }

        await EjecutarAsync(async () =>
        {
            await _caja.AbrirAsync(monto, null).ConfigureAwait(true);

            _dialogos.Notificar($"Caja abierta con una base de {Formatos.Moneda(monto)}.");
            WeakReferenceMessenger.Default.Send(new CajaCambiadaMensaje(true));

            await CargarAsync().ConfigureAwait(true);
        }, "No se pudo abrir la caja.");
    }

    [RelayCommand(CanExecute = nameof(PuedeOperar))]
    private Task RegistrarIngresoAsync() => RegistrarMovimientoAsync(esIngreso: true);

    [RelayCommand(CanExecute = nameof(PuedeOperar))]
    private Task RegistrarEgresoAsync() => RegistrarMovimientoAsync(esIngreso: false);

    private async Task RegistrarMovimientoAsync(bool esIngreso)
    {
        MovimientoCajaDialogoVistaModelo? dialogo = null;

        dialogo = new MovimientoCajaDialogoVistaModelo(
            _dialogos,
            async () =>
            {
                if (esIngreso)
                {
                    await _caja.RegistrarIngresoAsync(dialogo!.Monto, dialogo.Concepto).ConfigureAwait(true);
                }
                else
                {
                    await _caja.RegistrarEgresoAsync(dialogo!.Monto, dialogo.Concepto).ConfigureAwait(true);
                }
            },
            esIngreso,
            Arqueo?.MontoEsperado ?? 0);

        if (await _dialogos.MostrarAsync(dialogo).ConfigureAwait(true) is true)
        {
            _dialogos.Notificar(esIngreso ? "Ingreso registrado." : "Egreso registrado.");
            await CargarAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeOperar))]
    private async Task CerrarCajaAsync()
    {
        if (SesionAbierta is null || Arqueo is null)
        {
            return;
        }

        var arqueoActual = await _caja.CalcularArqueoAsync(SesionAbierta.Id).ConfigureAwait(true);
        Arqueo = arqueoActual;

        CierreCajaDialogoVistaModelo? dialogo = null;

        dialogo = new CierreCajaDialogoVistaModelo(
            _dialogos,
            async () => await _caja
                .CerrarAsync(SesionAbierta.Id, dialogo!.MontoContado, dialogo.Observaciones)
                .ConfigureAwait(true),
            arqueoActual);

        if (await _dialogos.MostrarAsync(dialogo).ConfigureAwait(true) is not true)
        {
            return;
        }

        _dialogos.Notificar("Caja cerrada correctamente.");
        WeakReferenceMessenger.Default.Send(new CajaCambiadaMensaje(false));

        await CargarAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ImprimirArqueoAsync()
    {
        var sesionId = SesionSeleccionada?.Id ?? SesionAbierta?.Id;

        if (sesionId is null)
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            var sesion = await _caja.ObtenerSesionAsync(sesionId.Value).ConfigureAwait(true);

            if (sesion is null)
            {
                return;
            }

            var arqueo = await _caja.CalcularArqueoAsync(sesionId.Value).ConfigureAwait(true);
            var movimientos = await _caja.ObtenerMovimientosAsync(sesionId.Value).ConfigureAwait(true);

            var ruta = await _documentos
                .GenerarArqueoCajaAsync(sesion, arqueo, movimientos)
                .ConfigureAwait(true);

            _archivos.AbrirConAplicacionPredeterminada(ruta);
        }, "No se pudo generar el arqueo de caja.");
    }

    [RelayCommand]
    private Task ActualizarAsync() => CargarAsync();
}
