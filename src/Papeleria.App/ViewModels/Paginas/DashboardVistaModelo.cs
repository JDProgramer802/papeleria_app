using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Papeleria.App.Infrastructure;
using Papeleria.Business.Dtos;
using Papeleria.Business.Services;
using Papeleria.Domain.Constants;

namespace Papeleria.App.ViewModels.Paginas;

/// <summary>Panel de inicio con los indicadores, gráficos y alertas del negocio.</summary>
public partial class DashboardVistaModelo : PaginaVistaModelo
{
    private readonly IServicioDashboard _dashboard;

    public DashboardVistaModelo(IServicioDashboard dashboard)
    {
        _dashboard = dashboard;

        Titulo = "Dashboard";
        Subtitulo = "Resumen del estado actual del negocio";

        // El panel se refresca solo cuando cambia algo que afecta a sus cifras.
        var mensajero = WeakReferenceMessenger.Default;

        mensajero.Register<DashboardVistaModelo, VentaRegistradaMensaje>(this, (d, m) => { _ = d.CargarAsync(); });
        mensajero.Register<DashboardVistaModelo, CompraRegistradaMensaje>(this, (d, m) => { _ = d.CargarAsync(); });
        mensajero.Register<DashboardVistaModelo, InventarioCambiadoMensaje>(this, (d, m) => { _ = d.CargarAsync(); });
        mensajero.Register<DashboardVistaModelo, CajaCambiadaMensaje>(this, (d, m) => { _ = d.CargarAsync(); });
    }

    public override string Modulo => Modulos.Dashboard;

    [ObservableProperty]
    private ResumenDashboardDto? _resumen;

    public ObservableCollection<PuntoSerie> SerieVentas { get; } = new();

    public ObservableCollection<PuntoSerie> SerieCompras { get; } = new();

    public ObservableCollection<ProductoVendidoDto> ProductosMasVendidos { get; } = new();

    public ObservableCollection<MovimientoKardexDto> MovimientosRecientes { get; } = new();

    public ObservableCollection<AlertaDto> Alertas { get; } = new();

    [ObservableProperty]
    private string _ultimaActualizacion = string.Empty;

    public override Task CargarAsync() => EjecutarAsync(async () =>
    {
        var resumen = await _dashboard.ObtenerResumenAsync().ConfigureAwait(true);

        Resumen = resumen;

        Reemplazar(SerieVentas, resumen.SerieVentas);
        Reemplazar(SerieCompras, resumen.SerieCompras);
        Reemplazar(ProductosMasVendidos, resumen.ProductosMasVendidos);
        Reemplazar(MovimientosRecientes, resumen.MovimientosRecientes);
        Reemplazar(Alertas, resumen.Alertas);

        UltimaActualizacion =
            $"Actualizado a las {Business.Common.Formatos.Hora(resumen.GeneradoEn)}";
    }, "No se pudieron cargar los indicadores del dashboard.");

    private static void Reemplazar<T>(ObservableCollection<T> destino, IEnumerable<T> origen)
    {
        destino.Clear();

        foreach (var elemento in origen)
        {
            destino.Add(elemento);
        }
    }

    /// <summary>Las tarjetas y alertas navegan al módulo correspondiente al pulsarlas.</summary>
    [RelayCommand]
    private static void IrAModulo(string? modulo)
    {
        if (!string.IsNullOrWhiteSpace(modulo))
        {
            WeakReferenceMessenger.Default.Send(new NavegarMensaje(modulo));
        }
    }

    [RelayCommand]
    private Task ActualizarAsync() => CargarAsync();
}
