using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Papeleria.App.Infrastructure;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;
using Papeleria.Business.Security;
using Papeleria.Business.Services;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Enums;
using Papeleria.Domain.Exceptions;

namespace Papeleria.App.ViewModels.Paginas;

public partial class GastosVistaModelo : PaginaVistaModelo
{
    private readonly IServicioGastos _gastos;
    private readonly IServicioDialogos _dialogos;
    private readonly IServicioArchivos _archivos;
    private readonly IServicioExportacion _exportacion;
    private readonly IContextoSesion _sesion;

    public GastosVistaModelo(IServicioGastos gastos, IServicioDialogos dialogos,
        IServicioArchivos archivos, IServicioExportacion exportacion, IContextoSesion sesion)
    {
        _gastos = gastos; _dialogos = dialogos; _archivos = archivos; _exportacion = exportacion; _sesion = sesion;
        Titulo = "Gastos del negocio";
        Subtitulo = "Organiza tus pagos y conoce en qué se va el dinero.";
    }

    public override string Modulo => Modulos.Gastos;
    public bool PuedeCrear => _sesion.Puede(Modulo, AccionPermiso.Crear);
    public bool PuedeAnular => _sesion.Puede(Modulo, AccionPermiso.Eliminar);
    public string[] Categorias { get; } = ["Arriendo", "Servicios públicos", "Nómina", "Transporte", "Mantenimiento", "Insumos de uso interno", "Publicidad", "Otros"];
    public IEnumerable<string> CategoriasFiltro => new[] { "Todas las categorías" }.Concat(Categorias);
    public IEnumerable<OpcionEnum<MetodoPago>> MetodosPago => Enumeraciones.Opciones<MetodoPago>()
        .Where(p => p.Valor is not (MetodoPago.Credito or MetodoPago.Mixto));
    public ObservableCollection<GastoDto> Gastos { get; } = new();
    [ObservableProperty] private DateTime? _desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _hasta = DateTime.Today;
    [ObservableProperty] private string _textoBusqueda = "";
    [ObservableProperty] private string _categoriaFiltro = "Todas las categorías";
    [ObservableProperty] private bool _incluirAnulados;
    [ObservableProperty] private GastoDto? _seleccionado;
    [ObservableProperty] private DateTime? _fecha = DateTime.Today;
    [ObservableProperty] private string _concepto = "";
    [ObservableProperty] private string _categoria = "Otros";
    [ObservableProperty] private string _monto = "";
    [ObservableProperty] private MetodoPago _metodoPago = MetodoPago.Efectivo;
    [ObservableProperty] private string _beneficiario = "";
    [ObservableProperty] private string _referencia = "";
    [ObservableProperty] private bool _formularioAbierto;
    [ObservableProperty] private decimal _total;
    [ObservableProperty] private decimal _efectivo;
    [ObservableProperty] private decimal _otrosPagos;
    [ObservableProperty] private string _resumen = "";
    public bool SinResultados => Gastos.Count == 0;

    private FiltroGastos Filtro()
    {
        if (Desde is null || Hasta is null) throw new NegocioException("Seleccione las dos fechas del período.");
        return new(Desde.Value, Hasta.Value, TextoBusqueda,
            CategoriaFiltro == "Todas las categorías" ? null : CategoriaFiltro, IncluirAnulados);
    }

    public override Task CargarAsync() => BuscarAsync();
    [RelayCommand] private Task BuscarAsync() => EjecutarAsync(CargarListadoAsync);

    private async Task CargarListadoAsync()
    {
        var lista = await _gastos.BuscarAsync(Filtro());
        Gastos.Clear();
        foreach (var gasto in lista) Gastos.Add(gasto);
        Seleccionado = null;
        var vigentes = lista.Where(g => !g.Anulado).ToList();
        Total = vigentes.Sum(g => g.Monto);
        Efectivo = vigentes.Where(g => g.MetodoPago == MetodoPago.Efectivo).Sum(g => g.Monto);
        OtrosPagos = Total - Efectivo;
        Resumen = $"{vigentes.Count} gastos vigentes · {lista.Count(g => g.Anulado)} anulados visibles";
        OnPropertyChanged(nameof(SinResultados));
    }

    [RelayCommand] private async Task EsteMesAsync()
    {
        Desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        Hasta = DateTime.Today;
        await BuscarAsync();
    }

    [RelayCommand] private void Nuevo()
    {
        if (!PuedeCrear) return;
        Fecha = DateTime.Today; Concepto = ""; Categoria = "Otros"; Monto = "";
        MetodoPago = MetodoPago.Efectivo; Beneficiario = ""; Referencia = "";
        MensajeError = null; FormularioAbierto = true;
    }
    [RelayCommand] private void Cancelar() => FormularioAbierto = false;

    [RelayCommand] private Task GuardarAsync() => EjecutarAsync(async () =>
    {
        if (Fecha is null) throw new NegocioException("Seleccione la fecha del gasto.");
        if (!decimal.TryParse(Monto, System.Globalization.NumberStyles.Number, Formatos.Cultura, out var importe))
            throw new NegocioException("Escriba un valor válido en pesos; use coma para los decimales.");
        var id = await _gastos.RegistrarAsync(new(Fecha.Value, Concepto, Categoria, importe, MetodoPago, Beneficiario, Referencia));
        FormularioAbierto = false;
        _dialogos.Notificar($"Gasto #{id} registrado correctamente.");
        await CargarListadoAsync();
    });

    [RelayCommand] private Task AnularAsync() => EjecutarAsync(async () =>
    {
        var gasto = Seleccionado;
        if (gasto is null || gasto.Anulado) throw new NegocioException("Seleccione un gasto vigente para anular.");
        var aviso = gasto.MetodoPago == MetodoPago.Efectivo
            ? "El importe se reintegrará al efectivo de la caja abierta. Confirme únicamente si el dinero ha regresado al cajón."
            : "Confirme únicamente si el pago fue cancelado o reembolsado. El registro se conservará como anulado.";
        if (!await _dialogos.ConfirmarAsync($"Anular gasto #{gasto.Id}", aviso, "Continuar", esDestructivo: true)) return;
        var motivo = await _dialogos.PedirTextoAsync("Motivo de anulación", "Explique por qué se anula este gasto", multilinea: true);
        if (motivo is null) return;
        await _gastos.AnularAsync(gasto.Id, motivo);
        await CargarListadoAsync();
        _dialogos.Notificar("Gasto anulado; se conservó su historial.");
    });

    [RelayCommand] private Task ExportarAsync() => EjecutarAsync(async () =>
    {
        var filtro = Filtro();
        var lista = await _gastos.BuscarAsync(filtro);
        var reporte = new ReporteTabular
        {
            Titulo = "Gastos del negocio", Periodo = $"{filtro.Desde:dd/MM/yyyy} – {filtro.Hasta:dd/MM/yyyy}",
            Subtitulo = "Los totales excluyen los gastos anulados.",
            Columnas = new ColumnaReporte[]
            {
                new() { Titulo = "N.º", Tipo = TipoColumna.Entero }, new() { Titulo = "Fecha", Tipo = TipoColumna.Fecha },
                new() { Titulo = "Concepto" }, new() { Titulo = "Categoría" }, new() { Titulo = "Beneficiario" },
                new() { Titulo = "Pago" }, new() { Titulo = "Referencia" }, new() { Titulo = "Monto", Tipo = TipoColumna.Moneda },
                new() { Titulo = "Estado" }, new() { Titulo = "Registrado por" }, new() { Titulo = "Motivo de anulación" }
            },
            Filas = lista.Select(g => new object?[] { g.Id, g.Fecha, g.Concepto, g.Categoria, g.Beneficiario, g.Pago, g.Referencia, g.Monto, g.Estado, g.Usuario, g.MotivoAnulacion }).ToList(),
            Indicadores = new[] { new IndicadorReporte("Total vigente", Formatos.Moneda(lista.Where(g => !g.Anulado).Sum(g => g.Monto))) }
        };
        var ruta = _archivos.SeleccionarDondeGuardar("Exportar gastos", "Excel|*.xlsx", $"Gastos-{DateTime.Today:yyyyMMdd}.xlsx");
        if (string.IsNullOrWhiteSpace(ruta)) return;
        await _exportacion.ExportarAsync(reporte, FormatoExportacion.Excel, ruta);
        _dialogos.Notificar("Gastos exportados correctamente.");
    });
}
