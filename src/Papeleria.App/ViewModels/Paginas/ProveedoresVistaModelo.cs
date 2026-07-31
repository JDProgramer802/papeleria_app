using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Papeleria.App.Infrastructure;
using Papeleria.App.ViewModels.Dialogos;
using Papeleria.Business.Dtos;
using Papeleria.Business.Security;
using Papeleria.Business.Services;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Entities;

namespace Papeleria.App.ViewModels.Paginas;

/// <summary>Directorio de proveedores con su historial de compras.</summary>
public partial class ProveedoresVistaModelo : PaginaVistaModelo
{
    private readonly IServicioProveedores _proveedores;
    private readonly IServicioDialogos _dialogos;
    private readonly IContextoSesion _sesion;

    public ProveedoresVistaModelo(
        IServicioProveedores proveedores,
        IServicioDialogos dialogos,
        IContextoSesion sesion)
    {
        _proveedores = proveedores;
        _dialogos = dialogos;
        _sesion = sesion;

        Titulo = "Proveedores";
        Subtitulo = "Directorio de proveedores e histórico de compras";

        Paginador.PaginaCambiada += (_, _) => _ = BuscarAsync();
    }

    public override string Modulo => Modulos.Proveedores;

    public Paginador Paginador { get; } = new();

    public ObservableCollection<Proveedor> Proveedores { get; } = new();

    public ObservableCollection<CompraResumenDto> Historial { get; } = new();

    [ObservableProperty] private string? _textoBusqueda;
    [ObservableProperty] private bool _soloActivos = true;
    [ObservableProperty] private Proveedor? _proveedorSeleccionado;
    [ObservableProperty] private ResumenTerceroDto? _resumenSeleccionado;

    public bool PuedeCrear => _sesion.Puede(Modulos.Proveedores, AccionPermiso.Crear);

    public bool PuedeEditar => _sesion.Puede(Modulos.Proveedores, AccionPermiso.Editar);

    public bool PuedeEliminar => _sesion.Puede(Modulos.Proveedores, AccionPermiso.Eliminar);

    public bool HaySeleccion => ProveedorSeleccionado is not null;

    partial void OnTextoBusquedaChanged(string? value)
    {
        Paginador.Reiniciar();
        _ = BuscarAsync();
    }

    partial void OnSoloActivosChanged(bool value)
    {
        Paginador.Reiniciar();
        _ = BuscarAsync();
    }

    partial void OnProveedorSeleccionadoChanged(Proveedor? value)
    {
        OnPropertyChanged(nameof(HaySeleccion));
        _ = CargarHistorialAsync();
    }

    public override Task CargarAsync() => BuscarAsync();

    [RelayCommand]
    private Task BuscarAsync() => EjecutarAsync(async () =>
    {
        var resultado = await _proveedores
            .BuscarAsync(TextoBusqueda, SoloActivos, Paginador.Pagina, Paginador.TamanoPagina)
            .ConfigureAwait(true);

        Proveedores.Clear();

        foreach (var proveedor in resultado.Elementos)
        {
            Proveedores.Add(proveedor);
        }

        Paginador.Actualizar(resultado);
    }, "No se pudo consultar el directorio de proveedores.");

    private Task CargarHistorialAsync() => EjecutarAsync(async () =>
    {
        Historial.Clear();
        ResumenSeleccionado = null;

        if (ProveedorSeleccionado is null)
        {
            return;
        }

        var compras = await _proveedores
            .ObtenerHistorialAsync(ProveedorSeleccionado.Id)
            .ConfigureAwait(true);

        foreach (var compra in compras)
        {
            Historial.Add(compra);
        }

        ResumenSeleccionado = await _proveedores
            .ObtenerResumenAsync(ProveedorSeleccionado.Id)
            .ConfigureAwait(true);
    }, "No se pudo cargar el historial del proveedor.");

    [RelayCommand]
    private Task NuevoAsync() => AbrirFormularioAsync(new Proveedor { Activo = true }, true);

    [RelayCommand]
    private Task EditarAsync() =>
        ProveedorSeleccionado is null ? Task.CompletedTask : AbrirFormularioAsync(ProveedorSeleccionado, false);

    private async Task AbrirFormularioAsync(Proveedor proveedor, bool esNuevo)
    {
        if (esNuevo ? !PuedeCrear : !PuedeEditar)
        {
            return;
        }

        ProveedorDialogoVistaModelo? dialogo = null;

        dialogo = new ProveedorDialogoVistaModelo(
            _dialogos,
            async () =>
            {
                var destino = esNuevo ? new Proveedor() : proveedor;

                destino.Nombre = dialogo!.Nombre;
                destino.Nit = dialogo.Nit;
                destino.Contacto = dialogo.Contacto;
                destino.Telefono = dialogo.Telefono;
                destino.Correo = dialogo.Correo;
                destino.Direccion = dialogo.Direccion;
                destino.Ciudad = dialogo.Ciudad;
                destino.Observaciones = dialogo.Observaciones;
                destino.Activo = dialogo.Activo;

                if (esNuevo)
                {
                    await _proveedores.CrearAsync(destino).ConfigureAwait(true);
                }
                else
                {
                    await _proveedores.ActualizarAsync(destino).ConfigureAwait(true);
                }
            },
            esNuevo ? "Nuevo proveedor" : "Editar proveedor")
        {
            Nombre = proveedor.Nombre,
            Nit = proveedor.Nit,
            Contacto = proveedor.Contacto,
            Telefono = proveedor.Telefono,
            Correo = proveedor.Correo,
            Direccion = proveedor.Direccion,
            Ciudad = proveedor.Ciudad,
            Observaciones = proveedor.Observaciones,
            Activo = proveedor.Activo
        };

        if (await _dialogos.MostrarAsync(dialogo).ConfigureAwait(true) is true)
        {
            _dialogos.Notificar(esNuevo ? "Proveedor creado." : "Proveedor actualizado.");
            await BuscarAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task EliminarAsync()
    {
        if (ProveedorSeleccionado is null || !PuedeEliminar)
        {
            return;
        }

        var confirmado = await _dialogos.ConfirmarAsync(
            "Eliminar proveedor",
            $"¿Desea eliminar a «{ProveedorSeleccionado.Nombre}»?\n\n" +
            "Si tiene compras registradas, se marcará como inactivo para conservar el histórico.",
            "Eliminar", esDestructivo: true).ConfigureAwait(true);

        if (!confirmado)
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            await _proveedores.EliminarAsync(ProveedorSeleccionado.Id).ConfigureAwait(true);
            _dialogos.Notificar("Proveedor eliminado.");
            await BuscarAsync().ConfigureAwait(true);
        }, "No se pudo eliminar el proveedor.");
    }

    [RelayCommand]
    private async Task AlternarEstadoAsync()
    {
        if (ProveedorSeleccionado is null || !PuedeEditar)
        {
            return;
        }

        var activar = !ProveedorSeleccionado.Activo;

        await EjecutarAsync(async () =>
        {
            await _proveedores.CambiarEstadoAsync(ProveedorSeleccionado.Id, activar).ConfigureAwait(true);
            _dialogos.Notificar(activar ? "Proveedor activado." : "Proveedor desactivado.");
            await BuscarAsync().ConfigureAwait(true);
        }, "No se pudo cambiar el estado del proveedor.");
    }
}
