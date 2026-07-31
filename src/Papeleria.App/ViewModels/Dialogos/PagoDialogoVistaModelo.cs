using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Papeleria.App.Infrastructure;
using Papeleria.Business.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.App.ViewModels.Dialogos;

/// <summary>
/// Diálogo de cobro del punto de venta: medio de pago, efectivo recibido y cambio.
/// Ofrece importes sugeridos para agilizar el cobro en efectivo.
/// </summary>
public partial class PagoDialogoVistaModelo : VistaModeloBase
{
    private readonly IServicioDialogos _dialogos;

    public PagoDialogoVistaModelo(IServicioDialogos dialogos, decimal total)
    {
        _dialogos = dialogos;

        Titulo = "Cobrar venta";
        Total = total;
        MontoRecibido = total;

        MetodosPago = new ObservableCollection<OpcionEnum<MetodoPago>>(
            Enumeraciones.Opciones<MetodoPago>().Where(o => o.Valor != MetodoPago.Credito));

        Sugerencias = new ObservableCollection<decimal>(CalcularSugerencias(total));
    }

    public ObservableCollection<OpcionEnum<MetodoPago>> MetodosPago { get; }

    /// <summary>Importes redondeados que suele entregar el cliente.</summary>
    public ObservableCollection<decimal> Sugerencias { get; }

    public decimal Total { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequiereEfectivo))]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmar))]
    private MetodoPago _metodoPago = MetodoPago.Efectivo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cambio))]
    [NotifyPropertyChangedFor(nameof(FaltaPorPagar))]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmar))]
    private decimal _montoRecibido;

    [ObservableProperty]
    private bool _imprimirFactura = true;

    public bool RequiereEfectivo => MetodoPago is MetodoPago.Efectivo or MetodoPago.Mixto;

    public decimal Cambio => RequiereEfectivo ? Dinero.Redondear(Math.Max(MontoRecibido - Total, 0)) : 0;

    public decimal FaltaPorPagar =>
        MetodoPago == MetodoPago.Efectivo ? Dinero.Redondear(Math.Max(Total - MontoRecibido, 0)) : 0;

    /// <summary>En efectivo no se puede cerrar la venta si el importe entregado no alcanza.</summary>
    public bool PuedeConfirmar => MetodoPago != MetodoPago.Efectivo || MontoRecibido >= Total;

    /// <summary>
    /// Propone el importe exacto y los billetes redondeados inmediatamente superiores,
    /// que es como se cobra en mostrador.
    /// </summary>
    private static IEnumerable<decimal> CalcularSugerencias(decimal total)
    {
        var propuestas = new List<decimal> { total };

        foreach (var escala in new decimal[] { 1_000, 5_000, 10_000, 20_000, 50_000, 100_000 })
        {
            var redondeado = Math.Ceiling(total / escala) * escala;

            if (redondeado > total && !propuestas.Contains(redondeado))
            {
                propuestas.Add(redondeado);
            }
        }

        return propuestas.Take(5);
    }

    [RelayCommand]
    private void UsarSugerencia(decimal monto) => MontoRecibido = monto;

    [RelayCommand]
    private void Confirmar()
    {
        if (!PuedeConfirmar)
        {
            MensajeError = "El efectivo recibido no cubre el total de la venta.";
            return;
        }

        _dialogos.Cerrar(true);
    }

    [RelayCommand]
    private void Cancelar() => _dialogos.Cerrar(false);
}
