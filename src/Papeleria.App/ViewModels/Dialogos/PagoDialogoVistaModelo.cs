using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Papeleria.App.Infrastructure;
using Papeleria.Business.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.App.ViewModels.Dialogos;

/// <summary>
/// Situación crediticia del cliente al que se le va a facturar, para decidir en el
/// mismo cobro si se le puede fiar y por cuánto.
/// </summary>
/// <param name="Nombre">Nombre del cliente, para explicar el aviso.</param>
/// <param name="Admite">Falso para el consumidor final o para quien no tiene cupo.</param>
/// <param name="CupoDisponible">Lo que todavía se le puede fiar sin pasarse del cupo.</param>
public record CreditoCliente(string Nombre, bool Admite, decimal CupoDisponible);

/// <summary>
/// Diálogo de cobro del punto de venta: medio de pago, efectivo recibido y cambio.
/// Ofrece importes sugeridos para agilizar el cobro en efectivo.
/// </summary>
public partial class PagoDialogoVistaModelo : VistaModeloBase
{
    private readonly IServicioDialogos _dialogos;

    public PagoDialogoVistaModelo(
        IServicioDialogos dialogos, decimal total, CreditoCliente? credito = null)
    {
        _dialogos = dialogos;

        Titulo = "Cobrar venta";
        Total = total;
        MontoRecibido = total;
        Credito = credito;

        // El crédito se ofrece siempre: si el cliente no lo admite, el diálogo explica
        // por qué en vez de esconder la opción y dejar al cajero sin saber qué pasa.
        MetodosPago = new ObservableCollection<OpcionEnum<MetodoPago>>(
            Enumeraciones.Opciones<MetodoPago>());

        Sugerencias = new ObservableCollection<decimal>(CalcularSugerencias(total));
    }

    /// <summary>Cupo del cliente; nulo cuando no se pudo consultar.</summary>
    public CreditoCliente? Credito { get; }

    public ObservableCollection<OpcionEnum<MetodoPago>> MetodosPago { get; }

    /// <summary>Importes redondeados que suele entregar el cliente.</summary>
    public ObservableCollection<decimal> Sugerencias { get; }

    public decimal Total { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequiereEfectivo))]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmar))]
    [NotifyPropertyChangedFor(nameof(EsCredito))]
    [NotifyPropertyChangedFor(nameof(PuedeFiar))]
    [NotifyPropertyChangedFor(nameof(AvisoCredito))]
    [NotifyPropertyChangedFor(nameof(TieneAvisoCredito))]
    [NotifyPropertyChangedFor(nameof(AdmiteReferencia))]
    [NotifyPropertyChangedFor(nameof(EtiquetaReferencia))]
    private MetodoPago _metodoPago = MetodoPago.Efectivo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cambio))]
    [NotifyPropertyChangedFor(nameof(FaltaPorPagar))]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmar))]
    private decimal _montoRecibido;

    [ObservableProperty]
    private bool _imprimirFactura = true;

    /// <summary>Aprobación del datáfono, referencia de la transferencia o teléfono de Nequi.</summary>
    [ObservableProperty]
    private string? _referenciaPago;

    public bool RequiereEfectivo => MetodoPago is MetodoPago.Efectivo or MetodoPago.Mixto;

    /// <summary>
    /// Los pagos que llegan a una cuenta piden referencia. No es obligatoria —a veces no
    /// alcanza el tiempo en el mostrador— pero sin ella cuadrar el extracto es adivinar.
    /// </summary>
    public bool AdmiteReferencia => MetodoPago is
        MetodoPago.Tarjeta or MetodoPago.Transferencia or MetodoPago.Nequi or MetodoPago.Daviplata;

    public string EtiquetaReferencia => MetodoPago switch
    {
        MetodoPago.Tarjeta => "Número de aprobación (opcional)",
        MetodoPago.Nequi => "Teléfono o referencia de Nequi (opcional)",
        MetodoPago.Daviplata => "Teléfono o referencia de Daviplata (opcional)",
        _ => "Referencia de la transferencia (opcional)"
    };

    public bool EsCredito => MetodoPago == MetodoPago.Credito;

    /// <summary>El cliente admite crédito y le alcanza el cupo para esta venta.</summary>
    public bool PuedeFiar => Credito is { Admite: true } && Total <= Credito.CupoDisponible;

    /// <summary>Lo que quedaría de cupo si la venta se fía.</summary>
    public decimal CupoRestante =>
        Credito is null ? 0 : Dinero.Redondear(Math.Max(Credito.CupoDisponible - Total, 0));

    /// <summary>Explica por qué no se puede fiar, en lugar de dejar el botón muerto.</summary>
    public string AvisoCredito
    {
        get
        {
            if (!EsCredito || PuedeFiar)
            {
                return string.Empty;
            }

            if (Credito is null)
            {
                return "No se pudo consultar el cupo del cliente.";
            }

            if (!Credito.Admite)
            {
                return $"{Credito.Nombre} no tiene cupo de crédito asignado. " +
                       "Edite su ficha en Clientes para autorizarle uno.";
            }

            return $"El cupo disponible de {Credito.Nombre} es " +
                   $"{Formatos.Moneda(Credito.CupoDisponible)} y la venta suma " +
                   $"{Formatos.Moneda(Total)}.";
        }
    }

    public bool TieneAvisoCredito => !string.IsNullOrEmpty(AvisoCredito);

    public decimal Cambio => RequiereEfectivo ? Dinero.Redondear(Math.Max(MontoRecibido - Total, 0)) : 0;

    public decimal FaltaPorPagar =>
        MetodoPago == MetodoPago.Efectivo ? Dinero.Redondear(Math.Max(Total - MontoRecibido, 0)) : 0;

    /// <summary>
    /// En efectivo no se puede cerrar si el importe entregado no alcanza; a crédito, si
    /// el cliente no tiene cupo suficiente.
    /// </summary>
    public bool PuedeConfirmar => MetodoPago switch
    {
        MetodoPago.Efectivo => MontoRecibido >= Total,
        MetodoPago.Credito => PuedeFiar,
        _ => true
    };

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
            MensajeError = EsCredito
                ? AvisoCredito
                : "El efectivo recibido no cubre el total de la venta.";

            return;
        }

        _dialogos.Cerrar(true);
    }

    [RelayCommand]
    private void Cancelar() => _dialogos.Cerrar(false);
}
