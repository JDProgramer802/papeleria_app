using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Papeleria.Business.Common;
using Papeleria.Domain.Common;

namespace Papeleria.App.Infrastructure;

/// <summary>
/// Estado de paginación reutilizable por todas las grillas. Notifica con
/// <see cref="PaginaCambiada"/> para que el modelo de vista recargue los datos.
/// </summary>
public partial class Paginador : ObservableObject
{
    public static readonly int[] TamanosDisponibles = { 15, 25, 50, 100, 200 };

    [ObservableProperty]
    private int _pagina = 1;

    [ObservableProperty]
    private int _tamanoPagina = 25;

    [ObservableProperty]
    private int _totalRegistros;

    [ObservableProperty]
    private int _totalPaginas = 1;

    [ObservableProperty]
    private string _textoRango = "Sin registros";

    /// <summary>Se dispara cuando el usuario cambia de página o de tamaño.</summary>
    public event EventHandler? PaginaCambiada;

    public IReadOnlyList<int> Tamanos => TamanosDisponibles;

    public bool TieneAnterior => Pagina > 1;

    public bool TieneSiguiente => Pagina < TotalPaginas;

    /// <summary>Vuelca en el paginador los metadatos devueltos por la consulta.</summary>
    public void Actualizar<T>(ResultadoPaginado<T> resultado)
    {
        TotalRegistros = resultado.TotalRegistros;
        TotalPaginas = resultado.TotalPaginas;

        // Si al filtrar la página actual queda fuera de rango, se vuelve a la última válida.
        if (Pagina > TotalPaginas)
        {
            Pagina = TotalPaginas;
        }

        TextoRango = resultado.TotalRegistros == 0
            ? "Sin registros"
            : $"{resultado.PrimerRegistro}–{resultado.UltimoRegistro} de {Formatos.Entero(resultado.TotalRegistros)}";

        NotificarNavegacion();
    }

    /// <summary>Vuelve a la primera página; se usa al cambiar cualquier filtro.</summary>
    public void Reiniciar() => Pagina = 1;

    partial void OnTamanoPaginaChanged(int value)
    {
        Pagina = 1;
        PaginaCambiada?.Invoke(this, EventArgs.Empty);
    }

    private void NotificarNavegacion()
    {
        OnPropertyChanged(nameof(TieneAnterior));
        OnPropertyChanged(nameof(TieneSiguiente));
        PrimeraPaginaCommand.NotifyCanExecuteChanged();
        PaginaAnteriorCommand.NotifyCanExecuteChanged();
        PaginaSiguienteCommand.NotifyCanExecuteChanged();
        UltimaPaginaCommand.NotifyCanExecuteChanged();
    }

    private void IrA(int pagina)
    {
        var destino = Math.Clamp(pagina, 1, Math.Max(TotalPaginas, 1));

        if (destino == Pagina)
        {
            return;
        }

        Pagina = destino;
        NotificarNavegacion();
        PaginaCambiada?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(TieneAnterior))]
    private void PrimeraPagina() => IrA(1);

    [RelayCommand(CanExecute = nameof(TieneAnterior))]
    private void PaginaAnterior() => IrA(Pagina - 1);

    [RelayCommand(CanExecute = nameof(TieneSiguiente))]
    private void PaginaSiguiente() => IrA(Pagina + 1);

    [RelayCommand(CanExecute = nameof(TieneSiguiente))]
    private void UltimaPagina() => IrA(TotalPaginas);
}
