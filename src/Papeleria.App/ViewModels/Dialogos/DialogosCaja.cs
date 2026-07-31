using CommunityToolkit.Mvvm.ComponentModel;
using Papeleria.App.Infrastructure;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;

namespace Papeleria.App.ViewModels.Dialogos;

/// <summary>Formulario de ingreso o egreso de efectivo en la caja abierta.</summary>
public partial class MovimientoCajaDialogoVistaModelo : DialogoFormularioBase
{
    public MovimientoCajaDialogoVistaModelo(
        IServicioDialogos dialogos, Func<Task> guardar, bool esIngreso, decimal efectivoDisponible)
        : base(dialogos, guardar)
    {
        EsIngreso = esIngreso;
        EfectivoDisponible = efectivoDisponible;

        Titulo = esIngreso ? "Registrar ingreso" : "Registrar egreso";
        Subtitulo = esIngreso
            ? "Dinero que entra a la caja por un concepto distinto de una venta."
            : "Dinero que sale de la caja: pagos, retiros o gastos menores.";
    }

    public bool EsIngreso { get; }

    /// <summary>Efectivo teórico en el cajón; un egreso no puede superarlo.</summary>
    public decimal EfectivoDisponible { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeGuardar))]
    private decimal _monto;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeGuardar))]
    private string _concepto = string.Empty;

    public bool PuedeGuardar =>
        Monto > 0 &&
        !string.IsNullOrWhiteSpace(Concepto) &&
        (EsIngreso || Monto <= EfectivoDisponible);
}

/// <summary>Formulario de cierre de caja: compara lo esperado con lo contado.</summary>
public partial class CierreCajaDialogoVistaModelo : DialogoFormularioBase
{
    public CierreCajaDialogoVistaModelo(
        IServicioDialogos dialogos, Func<Task> guardar, ArqueoCajaDto arqueo)
        : base(dialogos, guardar)
    {
        Arqueo = arqueo;
        MontoContado = arqueo.MontoEsperado;

        Titulo = "Cierre de caja";
        Subtitulo = "Cuente el efectivo del cajón y registre el importe real.";
    }

    public ArqueoCajaDto Arqueo { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Diferencia))]
    [NotifyPropertyChangedFor(nameof(TextoDiferencia))]
    [NotifyPropertyChangedFor(nameof(HayDescuadre))]
    private decimal _montoContado;

    [ObservableProperty]
    private string? _observaciones;

    public decimal Diferencia => Dinero.Redondear(MontoContado - Arqueo.MontoEsperado);

    public bool HayDescuadre => Diferencia != 0;

    public string TextoDiferencia => Diferencia switch
    {
        0 => "La caja cuadra exactamente",
        < 0 => $"Faltante de {Formatos.Moneda(Math.Abs(Diferencia))}",
        _ => $"Sobrante de {Formatos.Moneda(Diferencia)}"
    };
}
