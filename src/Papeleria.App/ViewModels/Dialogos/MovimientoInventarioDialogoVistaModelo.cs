using CommunityToolkit.Mvvm.ComponentModel;
using Papeleria.App.Infrastructure;

namespace Papeleria.App.ViewModels.Dialogos;

/// <summary>Tipos de movimiento manual que ofrece el módulo de inventario.</summary>
public enum TipoMovimientoManual
{
    Entrada = 0,
    Salida = 1,
    Ajuste = 2,
    Transferencia = 3
}

/// <summary>
/// Formulario de movimiento manual de inventario. Muestra solo los campos que
/// corresponden al tipo elegido y anticipa el stock resultante.
/// </summary>
public partial class MovimientoInventarioDialogoVistaModelo : DialogoFormularioBase
{
    public MovimientoInventarioDialogoVistaModelo(
        IServicioDialogos dialogos,
        Func<Task> guardar,
        TipoMovimientoManual tipo,
        string nombreProducto,
        decimal stockActual,
        decimal costoActual,
        string? ubicacionActual)
        : base(dialogos, guardar)
    {
        Tipo = tipo;
        NombreProducto = nombreProducto;
        StockActual = stockActual;
        CostoUnitario = costoActual;
        UbicacionOrigen = ubicacionActual ?? string.Empty;
        StockReal = stockActual;

        Titulo = tipo switch
        {
            TipoMovimientoManual.Entrada => "Entrada de inventario",
            TipoMovimientoManual.Salida => "Salida de inventario",
            TipoMovimientoManual.Ajuste => "Ajuste de inventario",
            _ => "Transferencia de ubicación"
        };

        Subtitulo = tipo switch
        {
            TipoMovimientoManual.Entrada => "Registra unidades que ingresan sin pasar por una compra.",
            TipoMovimientoManual.Salida => "Registra unidades que salen sin pasar por una venta.",
            TipoMovimientoManual.Ajuste => "Deja el inventario en la cantidad realmente contada.",
            _ => "Traslada mercancía entre ubicaciones sin alterar el total."
        };
    }

    public TipoMovimientoManual Tipo { get; }

    public string NombreProducto { get; }

    public decimal StockActual { get; }

    public bool EsAjuste => Tipo == TipoMovimientoManual.Ajuste;

    public bool EsTransferencia => Tipo == TipoMovimientoManual.Transferencia;

    public bool UsaCantidad => Tipo != TipoMovimientoManual.Ajuste;

    public bool UsaCosto => Tipo == TipoMovimientoManual.Entrada;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StockResultante))]
    private decimal _cantidad = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StockResultante))]
    [NotifyPropertyChangedFor(nameof(DiferenciaAjuste))]
    private decimal _stockReal;

    [ObservableProperty] private decimal _costoUnitario;
    [ObservableProperty] private string _motivo = string.Empty;
    [ObservableProperty] private string? _documentoReferencia;
    [ObservableProperty] private string _ubicacionOrigen = string.Empty;
    [ObservableProperty] private string _ubicacionDestino = string.Empty;

    /// <summary>Existencias que quedarán tras aplicar el movimiento.</summary>
    public decimal StockResultante => Tipo switch
    {
        TipoMovimientoManual.Entrada => StockActual + Cantidad,
        TipoMovimientoManual.Salida => StockActual - Cantidad,
        TipoMovimientoManual.Ajuste => StockReal,
        _ => StockActual
    };

    public decimal DiferenciaAjuste => StockReal - StockActual;
}
