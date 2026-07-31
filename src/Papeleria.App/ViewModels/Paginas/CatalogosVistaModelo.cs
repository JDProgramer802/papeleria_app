using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Papeleria.App.Infrastructure;
using Papeleria.App.ViewModels.Dialogos;
using Papeleria.Business.Security;
using Papeleria.Business.Services.Catalogos;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Entities;

namespace Papeleria.App.ViewModels.Paginas;

/// <summary>
/// Mantenimiento de los catálogos simples: categorías, marcas y unidades de medida.
/// Los tres comparten la misma mecánica, por lo que se resuelven con un único formulario.
/// </summary>
public partial class CatalogosVistaModelo : PaginaVistaModelo
{
    private readonly IServicioCategorias _categorias;
    private readonly IServicioMarcas _marcas;
    private readonly IServicioUnidadesMedida _unidades;
    private readonly IServicioDialogos _dialogos;
    private readonly IContextoSesion _sesion;

    public CatalogosVistaModelo(
        IServicioCategorias categorias,
        IServicioMarcas marcas,
        IServicioUnidadesMedida unidades,
        IServicioDialogos dialogos,
        IContextoSesion sesion)
    {
        _categorias = categorias;
        _marcas = marcas;
        _unidades = unidades;
        _dialogos = dialogos;
        _sesion = sesion;

        Titulo = "Catálogos";
        Subtitulo = "Categorías, marcas y unidades de medida usadas por los productos";
    }

    public override string Modulo => Modulos.Catalogos;

    public ObservableCollection<Categoria> Categorias { get; } = new();

    public ObservableCollection<Marca> Marcas { get; } = new();

    public ObservableCollection<UnidadMedida> Unidades { get; } = new();

    [ObservableProperty] private Categoria? _categoriaSeleccionada;
    [ObservableProperty] private Marca? _marcaSeleccionada;
    [ObservableProperty] private UnidadMedida? _unidadSeleccionada;

    public bool PuedeCrear => _sesion.Puede(Modulos.Catalogos, AccionPermiso.Crear);

    public bool PuedeEditar => _sesion.Puede(Modulos.Catalogos, AccionPermiso.Editar);

    public bool PuedeEliminar => _sesion.Puede(Modulos.Catalogos, AccionPermiso.Eliminar);

    public override Task CargarAsync() => EjecutarAsync(async () =>
    {
        Reemplazar(Categorias, await _categorias.ListarAsync().ConfigureAwait(true));
        Reemplazar(Marcas, await _marcas.ListarAsync().ConfigureAwait(true));
        Reemplazar(Unidades, await _unidades.ListarAsync().ConfigureAwait(true));
    }, "No se pudieron cargar los catálogos.");

    private static void Reemplazar<T>(ObservableCollection<T> destino, IEnumerable<T> origen)
    {
        destino.Clear();

        foreach (var elemento in origen)
        {
            destino.Add(elemento);
        }
    }

    // ── Categorías ──────────────────────────────────────────────────────────

    [RelayCommand]
    private Task NuevaCategoriaAsync() => EditarCategoriaInternoAsync(new Categoria { Activo = true }, true);

    [RelayCommand]
    private Task EditarCategoriaAsync() =>
        CategoriaSeleccionada is null
            ? Task.CompletedTask
            : EditarCategoriaInternoAsync(CategoriaSeleccionada, false);

    private async Task EditarCategoriaInternoAsync(Categoria categoria, bool esNueva)
    {
        if (esNueva ? !PuedeCrear : !PuedeEditar)
        {
            return;
        }

        CatalogoDialogoVistaModelo? dialogo = null;

        dialogo = new CatalogoDialogoVistaModelo(
            _dialogos,
            async () =>
            {
                var destino = esNueva ? new Categoria() : categoria;
                destino.Nombre = dialogo!.Nombre;
                destino.Descripcion = dialogo.Descripcion;
                destino.Activo = dialogo.Activo;

                if (esNueva)
                {
                    await _categorias.CrearAsync(destino).ConfigureAwait(true);
                }
                else
                {
                    await _categorias.ActualizarAsync(destino).ConfigureAwait(true);
                }
            },
            esNueva ? "Nueva categoría" : "Editar categoría",
            usaAbreviatura: false)
        {
            Nombre = categoria.Nombre,
            Descripcion = categoria.Descripcion,
            Activo = categoria.Activo
        };

        if (await _dialogos.MostrarAsync(dialogo).ConfigureAwait(true) is true)
        {
            _dialogos.Notificar(esNueva ? "Categoría creada." : "Categoría actualizada.");
            await CargarAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private Task EliminarCategoriaAsync() =>
        EliminarAsync(CategoriaSeleccionada?.Nombre, "la categoría",
            () => _categorias.EliminarAsync(CategoriaSeleccionada!.Id));

    // ── Marcas ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private Task NuevaMarcaAsync() => EditarMarcaInternoAsync(new Marca { Activo = true }, true);

    [RelayCommand]
    private Task EditarMarcaAsync() =>
        MarcaSeleccionada is null ? Task.CompletedTask : EditarMarcaInternoAsync(MarcaSeleccionada, false);

    private async Task EditarMarcaInternoAsync(Marca marca, bool esNueva)
    {
        if (esNueva ? !PuedeCrear : !PuedeEditar)
        {
            return;
        }

        CatalogoDialogoVistaModelo? dialogo = null;

        dialogo = new CatalogoDialogoVistaModelo(
            _dialogos,
            async () =>
            {
                var destino = esNueva ? new Marca() : marca;
                destino.Nombre = dialogo!.Nombre;
                destino.Descripcion = dialogo.Descripcion;
                destino.Activo = dialogo.Activo;

                if (esNueva)
                {
                    await _marcas.CrearAsync(destino).ConfigureAwait(true);
                }
                else
                {
                    await _marcas.ActualizarAsync(destino).ConfigureAwait(true);
                }
            },
            esNueva ? "Nueva marca" : "Editar marca",
            usaAbreviatura: false)
        {
            Nombre = marca.Nombre,
            Descripcion = marca.Descripcion,
            Activo = marca.Activo
        };

        if (await _dialogos.MostrarAsync(dialogo).ConfigureAwait(true) is true)
        {
            _dialogos.Notificar(esNueva ? "Marca creada." : "Marca actualizada.");
            await CargarAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private Task EliminarMarcaAsync() =>
        EliminarAsync(MarcaSeleccionada?.Nombre, "la marca",
            () => _marcas.EliminarAsync(MarcaSeleccionada!.Id));

    // ── Unidades de medida ──────────────────────────────────────────────────

    [RelayCommand]
    private Task NuevaUnidadAsync() => EditarUnidadInternoAsync(new UnidadMedida { Activo = true }, true);

    [RelayCommand]
    private Task EditarUnidadAsync() =>
        UnidadSeleccionada is null ? Task.CompletedTask : EditarUnidadInternoAsync(UnidadSeleccionada, false);

    private async Task EditarUnidadInternoAsync(UnidadMedida unidad, bool esNueva)
    {
        if (esNueva ? !PuedeCrear : !PuedeEditar)
        {
            return;
        }

        CatalogoDialogoVistaModelo? dialogo = null;

        dialogo = new CatalogoDialogoVistaModelo(
            _dialogos,
            async () =>
            {
                var destino = esNueva ? new UnidadMedida() : unidad;
                destino.Nombre = dialogo!.Nombre;
                destino.Abreviatura = dialogo.Abreviatura;
                destino.Descripcion = dialogo.Descripcion;
                destino.Activo = dialogo.Activo;

                if (esNueva)
                {
                    await _unidades.CrearAsync(destino).ConfigureAwait(true);
                }
                else
                {
                    await _unidades.ActualizarAsync(destino).ConfigureAwait(true);
                }
            },
            esNueva ? "Nueva unidad de medida" : "Editar unidad de medida",
            usaAbreviatura: true)
        {
            Nombre = unidad.Nombre,
            Abreviatura = unidad.Abreviatura,
            Descripcion = unidad.Descripcion,
            Activo = unidad.Activo
        };

        if (await _dialogos.MostrarAsync(dialogo).ConfigureAwait(true) is true)
        {
            _dialogos.Notificar(esNueva ? "Unidad creada." : "Unidad actualizada.");
            await CargarAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private Task EliminarUnidadAsync() =>
        EliminarAsync(UnidadSeleccionada?.Nombre, "la unidad de medida",
            () => _unidades.EliminarAsync(UnidadSeleccionada!.Id));

    /// <summary>Confirmación y borrado común a los tres catálogos.</summary>
    private async Task EliminarAsync(string? nombre, string descripcionEntidad, Func<Task> eliminar)
    {
        if (string.IsNullOrWhiteSpace(nombre) || !PuedeEliminar)
        {
            return;
        }

        var confirmado = await _dialogos.ConfirmarAsync(
            "Eliminar registro",
            $"¿Desea eliminar {descripcionEntidad} «{nombre}»?\n\n" +
            "Si está en uso por algún producto, se desactivará en lugar de borrarse.",
            "Eliminar", esDestructivo: true).ConfigureAwait(true);

        if (!confirmado)
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            await eliminar().ConfigureAwait(true);
            _dialogos.Notificar("Registro eliminado.");
            await CargarAsync().ConfigureAwait(true);
        }, "No se pudo eliminar el registro.");
    }
}
