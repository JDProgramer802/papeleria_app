using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Papeleria.App.Infrastructure;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;

namespace Papeleria.App.ViewModels.Dialogos;

/// <summary>
/// Renglón de la factura mientras se decide cuánto devolver. La cantidad se ajusta
/// con los botones de más y menos, que en el mostrador es más rápido y seguro que
/// escribir un número.
/// </summary>
public partial class LineaDevolucionEditable : ObservableObject
{
    public required int ProductoId { get; init; }

    public required string Descripcion { get; init; }

    public required string UnidadAbreviatura { get; init; }

    public decimal CantidadVendida { get; init; }

    public decimal CantidadDevuelta { get; init; }

    public decimal ValorUnitario { get; init; }

    /// <summary>Máximo que todavía se puede devolver de este renglón.</summary>
    public decimal Disponible { get; init; }

    public bool ReponeInventario { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Importe))]
    [NotifyPropertyChangedFor(nameof(HayCantidad))]
    private decimal _cantidad;

    public decimal Importe => Dinero.Redondear(Cantidad * ValorUnitario);

    public bool HayCantidad => Cantidad > 0;

    public bool SePuedeDevolver => Disponible > 0;

    /// <summary>Aviso de lo ya devuelto antes, para no repetir el reintegro.</summary>
    public string DetalleTexto => CantidadDevuelta > 0
        ? $"Vendidas {CantidadVendida:N0} · ya devueltas {CantidadDevuelta:N0}"
        : $"Vendidas {CantidadVendida:N0}";

    [RelayCommand]
    private void Sumar()
    {
        if (Cantidad < Disponible)
        {
            Cantidad += 1;
        }
    }

    [RelayCommand]
    private void Restar()
    {
        if (Cantidad > 0)
        {
            Cantidad -= 1;
        }
    }

    [RelayCommand]
    private void Todo() => Cantidad = Disponible;
}

/// <summary>Formulario de devolución parcial de una factura.</summary>
public partial class DevolucionDialogoVistaModelo : DialogoFormularioBase
{
    public DevolucionDialogoVistaModelo(
        IServicioDialogos dialogos, Func<Task> guardar, VentaDevolvibleDto venta)
        : base(dialogos, guardar)
    {
        Titulo = "Devolver productos";
        Venta = venta;

        Lineas = new ObservableCollection<LineaDevolucionEditable>(
            venta.Lineas
                .Where(l => l.SePuedeDevolver)
                .Select(l => new LineaDevolucionEditable
                {
                    ProductoId = l.ProductoId,
                    Descripcion = l.Descripcion,
                    UnidadAbreviatura = l.UnidadAbreviatura,
                    CantidadVendida = l.CantidadVendida,
                    CantidadDevuelta = l.CantidadDevuelta,
                    ValorUnitario = l.ValorUnitario,
                    Disponible = l.Disponible,
                    ReponeInventario = l.ReponeInventario
                }));

        foreach (var linea in Lineas)
        {
            linea.PropertyChanged += (_, _) => NotificarTotales();
        }
    }

    public VentaDevolvibleDto Venta { get; }

    public ObservableCollection<LineaDevolucionEditable> Lineas { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeGuardar))]
    private string _motivo = string.Empty;

    public decimal TotalDevolver => Dinero.Redondear(Lineas.Sum(l => l.Importe));

    public string TotalDevolverTexto => Formatos.Moneda(TotalDevolver);

    public int LineasSeleccionadas => Lineas.Count(l => l.HayCantidad);

    /// <summary>Sin cantidad ni motivo no hay devolución que registrar.</summary>
    public bool PuedeGuardar => LineasSeleccionadas > 0 && !string.IsNullOrWhiteSpace(Motivo);

    public bool HayLineas => Lineas.Count > 0;

    /// <summary>Datos listos para el servicio.</summary>
    public List<LineaDevolucion> ObtenerLineas() =>
        Lineas.Where(l => l.HayCantidad)
              .Select(l => new LineaDevolucion { ProductoId = l.ProductoId, Cantidad = l.Cantidad })
              .ToList();

    private void NotificarTotales()
    {
        OnPropertyChanged(nameof(TotalDevolver));
        OnPropertyChanged(nameof(TotalDevolverTexto));
        OnPropertyChanged(nameof(LineasSeleccionadas));
        OnPropertyChanged(nameof(PuedeGuardar));
    }
}
