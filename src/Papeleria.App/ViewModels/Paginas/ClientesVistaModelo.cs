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

/// <summary>Directorio de clientes con su historial de compras.</summary>
public partial class ClientesVistaModelo : PaginaVistaModelo
{
    private readonly IServicioClientes _clientes;
    private readonly IServicioDialogos _dialogos;
    private readonly IContextoSesion _sesion;

    public ClientesVistaModelo(
        IServicioClientes clientes,
        IServicioDialogos dialogos,
        IContextoSesion sesion)
    {
        _clientes = clientes;
        _dialogos = dialogos;
        _sesion = sesion;

        Titulo = "Clientes";
        Subtitulo = "Directorio de clientes e histórico de facturas";

        Paginador.PaginaCambiada += (_, _) => _ = BuscarAsync();
    }

    public override string Modulo => Modulos.Clientes;

    public Paginador Paginador { get; } = new();

    public ObservableCollection<Cliente> Clientes { get; } = new();

    public ObservableCollection<VentaResumenDto> Historial { get; } = new();

    [ObservableProperty] private string? _textoBusqueda;
    [ObservableProperty] private bool _soloActivos = true;
    [ObservableProperty] private Cliente? _clienteSeleccionado;
    [ObservableProperty] private ResumenTerceroDto? _resumenSeleccionado;

    public bool PuedeCrear => _sesion.Puede(Modulos.Clientes, AccionPermiso.Crear);

    public bool PuedeEditar => _sesion.Puede(Modulos.Clientes, AccionPermiso.Editar);

    public bool PuedeEliminar => _sesion.Puede(Modulos.Clientes, AccionPermiso.Eliminar);

    public bool HaySeleccion => ClienteSeleccionado is not null;

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

    partial void OnClienteSeleccionadoChanged(Cliente? value)
    {
        OnPropertyChanged(nameof(HaySeleccion));
        _ = CargarHistorialAsync();
    }

    public override Task CargarAsync() => BuscarAsync();

    [RelayCommand]
    private Task BuscarAsync() => EjecutarAsync(async () =>
    {
        var resultado = await _clientes
            .BuscarAsync(TextoBusqueda, SoloActivos, Paginador.Pagina, Paginador.TamanoPagina)
            .ConfigureAwait(true);

        Clientes.Clear();

        foreach (var cliente in resultado.Elementos)
        {
            Clientes.Add(cliente);
        }

        Paginador.Actualizar(resultado);
    }, "No se pudo consultar el directorio de clientes.");

    private Task CargarHistorialAsync() => EjecutarAsync(async () =>
    {
        Historial.Clear();
        ResumenSeleccionado = null;

        if (ClienteSeleccionado is null)
        {
            return;
        }

        var ventas = await _clientes.ObtenerHistorialAsync(ClienteSeleccionado.Id).ConfigureAwait(true);

        foreach (var venta in ventas)
        {
            Historial.Add(venta);
        }

        ResumenSeleccionado = await _clientes
            .ObtenerResumenAsync(ClienteSeleccionado.Id)
            .ConfigureAwait(true);
    }, "No se pudo cargar el historial del cliente.");

    [RelayCommand]
    private Task NuevoAsync() => AbrirFormularioAsync(new Cliente { Activo = true }, true);

    [RelayCommand]
    private Task EditarAsync() =>
        ClienteSeleccionado is null ? Task.CompletedTask : AbrirFormularioAsync(ClienteSeleccionado, false);

    private async Task AbrirFormularioAsync(Cliente cliente, bool esNuevo)
    {
        if (esNuevo ? !PuedeCrear : !PuedeEditar)
        {
            return;
        }

        ClienteDialogoVistaModelo? dialogo = null;

        dialogo = new ClienteDialogoVistaModelo(
            _dialogos,
            async () =>
            {
                var destino = esNuevo ? new Cliente() : cliente;

                destino.Nombre = dialogo!.Nombre;
                destino.TipoDocumento = dialogo.TipoDocumento;
                destino.NumeroDocumento = dialogo.NumeroDocumento;
                destino.Telefono = dialogo.Telefono;
                destino.Correo = dialogo.Correo;
                destino.Direccion = dialogo.Direccion;
                destino.Ciudad = dialogo.Ciudad;
                destino.Observaciones = dialogo.Observaciones;
                destino.Activo = dialogo.Activo;

                if (esNuevo)
                {
                    await _clientes.CrearAsync(destino).ConfigureAwait(true);
                }
                else
                {
                    await _clientes.ActualizarAsync(destino).ConfigureAwait(true);
                }
            },
            esNuevo ? "Nuevo cliente" : "Editar cliente")
        {
            Nombre = cliente.Nombre,
            TipoDocumento = cliente.TipoDocumento,
            NumeroDocumento = cliente.NumeroDocumento,
            Telefono = cliente.Telefono,
            Correo = cliente.Correo,
            Direccion = cliente.Direccion,
            Ciudad = cliente.Ciudad,
            Observaciones = cliente.Observaciones,
            Activo = cliente.Activo,
            EsProtegido = cliente.EsProtegido
        };

        if (await _dialogos.MostrarAsync(dialogo).ConfigureAwait(true) is true)
        {
            _dialogos.Notificar(esNuevo ? "Cliente creado." : "Cliente actualizado.");
            await BuscarAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task EliminarAsync()
    {
        if (ClienteSeleccionado is null || !PuedeEliminar)
        {
            return;
        }

        var confirmado = await _dialogos.ConfirmarAsync(
            "Eliminar cliente",
            $"¿Desea eliminar a «{ClienteSeleccionado.Nombre}»?\n\n" +
            "Si tiene ventas registradas, se marcará como inactivo para conservar el histórico.",
            "Eliminar", esDestructivo: true).ConfigureAwait(true);

        if (!confirmado)
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            await _clientes.EliminarAsync(ClienteSeleccionado.Id).ConfigureAwait(true);
            _dialogos.Notificar("Cliente eliminado.");
            await BuscarAsync().ConfigureAwait(true);
        }, "No se pudo eliminar el cliente.");
    }

    [RelayCommand]
    private async Task AlternarEstadoAsync()
    {
        if (ClienteSeleccionado is null || !PuedeEditar)
        {
            return;
        }

        var activar = !ClienteSeleccionado.Activo;

        await EjecutarAsync(async () =>
        {
            await _clientes.CambiarEstadoAsync(ClienteSeleccionado.Id, activar).ConfigureAwait(true);
            _dialogos.Notificar(activar ? "Cliente activado." : "Cliente desactivado.");
            await BuscarAsync().ConfigureAwait(true);
        }, "No se pudo cambiar el estado del cliente.");
    }
}
