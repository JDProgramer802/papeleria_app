using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Papeleria.App.Ayuda;
using Papeleria.App.Infrastructure;
using Papeleria.Business.Dtos;
using Papeleria.Business.Services;
using Papeleria.Domain.Constants;

namespace Papeleria.App.ViewModels.Paginas;

/// <summary>
/// Manual de uso y tutorial guiado. El manual explica cada módulo; el tutorial mira
/// el estado real del negocio y señala qué falta por dejar montado.
/// </summary>
public partial class ManualVistaModelo : PaginaVistaModelo
{
    private readonly IServicioAyuda _ayuda;
    private readonly INavegacion _navegacion;
    private readonly IServicioDialogos _dialogos;

    public ManualVistaModelo(
        IServicioAyuda ayuda,
        INavegacion navegacion,
        IServicioDialogos dialogos)
    {
        _ayuda = ayuda;
        _navegacion = navegacion;
        _dialogos = dialogos;

        Titulo = "Manual de uso";
        Subtitulo = "Cómo usar el sistema y en qué orden montar su papelería";

        foreach (var seccion in ContenidoManual.Secciones)
        {
            Secciones.Add(seccion);
        }

        SeccionSeleccionada = Secciones.FirstOrDefault();
    }

    public override string Modulo => Modulos.Manual;

    public ObservableCollection<SeccionManual> Secciones { get; } = new();

    public ObservableCollection<PasoTutorialDto> Pasos { get; } = new();

    [ObservableProperty] private string? _busqueda;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeccion))]
    private SeccionManual? _seccionSeleccionada;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayProgreso))]
    private ProgresoTutorialDto? _progreso;

    /// <summary>Alterna entre el manual y el tutorial guiado.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrandoManual))]
    private bool _mostrandoTutorial;

    public bool MostrandoManual => !MostrandoTutorial;

    public bool HaySeccion => SeccionSeleccionada is not null;

    public bool HayProgreso => Progreso is not null;

    public bool SinResultados => Secciones.Count == 0;

    partial void OnBusquedaChanged(string? value) => Filtrar();

    /// <summary>
    /// Filtra por el texto completo del apartado, no solo por su título: quien busca
    /// «fiar» debe encontrar Cartera aunque esa palabra no esté en el nombre.
    /// </summary>
    private void Filtrar()
    {
        var texto = Busqueda?.Trim();

        var coincidencias = string.IsNullOrWhiteSpace(texto)
            ? ContenidoManual.Secciones
            : ContenidoManual.Secciones
                .Where(s => s.TextoBuscable.Contains(texto, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

        Secciones.Clear();

        foreach (var seccion in coincidencias)
        {
            Secciones.Add(seccion);
        }

        OnPropertyChanged(nameof(SinResultados));

        if (SeccionSeleccionada is null || !Secciones.Contains(SeccionSeleccionada))
        {
            SeccionSeleccionada = Secciones.FirstOrDefault();
        }
    }

    public override Task CargarAsync() => ActualizarProgresoAsync();

    [RelayCommand]
    private Task ActualizarProgresoAsync() => EjecutarAsync(async () =>
    {
        var progreso = await _ayuda.ObtenerProgresoAsync().ConfigureAwait(true);

        Progreso = progreso;

        Pasos.Clear();

        foreach (var paso in progreso.Pasos)
        {
            Pasos.Add(paso);
        }
    }, "No se pudo revisar el estado de la puesta en marcha.");

    [RelayCommand]
    private void VerTutorial() => MostrandoTutorial = true;

    [RelayCommand]
    private void VerManual() => MostrandoTutorial = false;

    /// <summary>Lleva al módulo donde se resuelve el paso.</summary>
    [RelayCommand]
    private async Task IrAlPasoAsync(PasoTutorialDto? paso)
    {
        if (paso is null)
        {
            return;
        }

        if (!_navegacion.PuedeNavegar(paso.Modulo))
        {
            await _dialogos.InformarAsync(
                "Sin acceso",
                $"Su usuario no tiene permiso para entrar a {Modulos.Nombres[paso.Modulo]}. " +
                "Pídale al administrador que lo haga.",
                esError: true).ConfigureAwait(true);

            return;
        }

        await _navegacion.NavegarAsync(paso.Modulo).ConfigureAwait(true);
    }

    [RelayCommand]
    private void LimpiarBusqueda() => Busqueda = null;
}
