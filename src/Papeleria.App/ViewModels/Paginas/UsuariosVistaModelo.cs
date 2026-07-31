using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Papeleria.App.Infrastructure;
using Papeleria.App.ViewModels.Dialogos;
using Papeleria.Business.Common;
using Papeleria.Business.Security;
using Papeleria.Business.Services;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Entities;
using Papeleria.Domain.Enums;

namespace Papeleria.App.ViewModels.Paginas;

/// <summary>Fila editable de la matriz de permisos.</summary>
public partial class PermisoEditable : ObservableObject
{
    public required string Modulo { get; init; }

    public required string NombreModulo { get; init; }

    [ObservableProperty] private bool _puedeVer;
    [ObservableProperty] private bool _puedeCrear;
    [ObservableProperty] private bool _puedeEditar;
    [ObservableProperty] private bool _puedeEliminar;
}

/// <summary>Administración de usuarios del sistema y de la matriz de permisos por rol.</summary>
public partial class UsuariosVistaModelo : PaginaVistaModelo
{
    private readonly IServicioUsuarios _usuarios;
    private readonly IServicioAutenticacion _autenticacion;
    private readonly IServicioDialogos _dialogos;
    private readonly IContextoSesion _sesion;

    public UsuariosVistaModelo(
        IServicioUsuarios usuarios,
        IServicioAutenticacion autenticacion,
        IServicioDialogos dialogos,
        IContextoSesion sesion)
    {
        _usuarios = usuarios;
        _autenticacion = autenticacion;
        _dialogos = dialogos;
        _sesion = sesion;

        Titulo = "Usuarios y permisos";
        Subtitulo = "Operadores del sistema y alcance de cada rol";

        Roles = new ObservableCollection<OpcionEnum<RolUsuario>>(
            Enumeraciones.Opciones<RolUsuario>().Where(o => o.Valor != RolUsuario.Administrador));

        RolPermisos = Roles.FirstOrDefault()?.Valor ?? RolUsuario.Cajero;
    }

    public override string Modulo => Modulos.Usuarios;

    public ObservableCollection<Usuario> Usuarios { get; } = new();

    public ObservableCollection<PermisoEditable> Permisos { get; } = new();

    /// <summary>Solo se editan los roles distintos de Administrador, que siempre tiene acceso total.</summary>
    public ObservableCollection<OpcionEnum<RolUsuario>> Roles { get; }

    [ObservableProperty] private Usuario? _usuarioSeleccionado;
    [ObservableProperty] private RolUsuario _rolPermisos;

    public bool PuedeCrear => _sesion.Puede(Modulos.Usuarios, AccionPermiso.Crear);

    public bool PuedeEditar => _sesion.Puede(Modulos.Usuarios, AccionPermiso.Editar);

    public bool PuedeEliminar => _sesion.Puede(Modulos.Usuarios, AccionPermiso.Eliminar);

    public bool HaySeleccion => UsuarioSeleccionado is not null;

    partial void OnUsuarioSeleccionadoChanged(Usuario? value) => OnPropertyChanged(nameof(HaySeleccion));

    partial void OnRolPermisosChanged(RolUsuario value) => _ = CargarPermisosAsync();

    public override async Task CargarAsync()
    {
        await CargarUsuariosAsync().ConfigureAwait(true);
        await CargarPermisosAsync().ConfigureAwait(true);
    }

    private Task CargarUsuariosAsync() => EjecutarAsync(async () =>
    {
        var usuarios = await _usuarios.ListarAsync().ConfigureAwait(true);

        Usuarios.Clear();

        foreach (var usuario in usuarios)
        {
            Usuarios.Add(usuario);
        }
    }, "No se pudo cargar la lista de usuarios.");

    private Task CargarPermisosAsync() => EjecutarAsync(async () =>
    {
        var permisos = await _usuarios.ObtenerPermisosAsync(RolPermisos).ConfigureAwait(true);

        Permisos.Clear();

        foreach (var permiso in permisos)
        {
            Permisos.Add(new PermisoEditable
            {
                Modulo = permiso.Modulo,
                NombreModulo = Modulos.Nombres.TryGetValue(permiso.Modulo, out var nombre)
                    ? nombre
                    : permiso.Modulo,
                PuedeVer = permiso.PuedeVer,
                PuedeCrear = permiso.PuedeCrear,
                PuedeEditar = permiso.PuedeEditar,
                PuedeEliminar = permiso.PuedeEliminar
            });
        }
    }, "No se pudieron cargar los permisos del rol.");

    [RelayCommand]
    private Task NuevoAsync() => AbrirFormularioAsync(new Usuario { Activo = true, Rol = RolUsuario.Cajero }, true);

    [RelayCommand]
    private Task EditarAsync() =>
        UsuarioSeleccionado is null ? Task.CompletedTask : AbrirFormularioAsync(UsuarioSeleccionado, false);

    private async Task AbrirFormularioAsync(Usuario usuario, bool esNuevo)
    {
        if (esNuevo ? !PuedeCrear : !PuedeEditar)
        {
            return;
        }

        UsuarioDialogoVistaModelo? dialogo = null;

        dialogo = new UsuarioDialogoVistaModelo(
            _dialogos,
            async () =>
            {
                if (esNuevo && dialogo!.Contrasena != dialogo.ConfirmacionContrasena)
                {
                    throw new Domain.Exceptions.NegocioException("Las contraseñas no coinciden.");
                }

                var destino = esNuevo ? new Usuario() : usuario;

                destino.Id = usuario.Id;
                destino.NombreUsuario = dialogo!.NombreUsuario;
                destino.NombreCompleto = dialogo.NombreCompleto;
                destino.Correo = dialogo.Correo;
                destino.Telefono = dialogo.Telefono;
                destino.Rol = dialogo.Rol;
                destino.Activo = dialogo.Activo;

                if (esNuevo)
                {
                    await _usuarios.CrearAsync(destino, dialogo.Contrasena).ConfigureAwait(true);
                }
                else
                {
                    await _usuarios.ActualizarAsync(destino).ConfigureAwait(true);
                }
            },
            esNuevo ? "Nuevo usuario" : "Editar usuario",
            esNuevo)
        {
            NombreUsuario = usuario.NombreUsuario,
            NombreCompleto = usuario.NombreCompleto,
            Correo = usuario.Correo,
            Telefono = usuario.Telefono,
            Rol = usuario.Rol,
            Activo = usuario.Activo,
            EsProtegido = usuario.EsProtegido
        };

        if (await _dialogos.MostrarAsync(dialogo).ConfigureAwait(true) is true)
        {
            _dialogos.Notificar(esNuevo ? "Usuario creado." : "Usuario actualizado.");
            await CargarUsuariosAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task RestablecerContrasenaAsync()
    {
        if (UsuarioSeleccionado is null || !PuedeEditar)
        {
            return;
        }

        var nueva = await _dialogos.PedirTextoAsync(
            $"Restablecer la contraseña de {UsuarioSeleccionado.NombreCompleto}",
            "Nueva contraseña (mínimo 6 caracteres, con letras y números)").ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(nueva))
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            await _usuarios.RestablecerContrasenaAsync(UsuarioSeleccionado.Id, nueva).ConfigureAwait(true);
            _dialogos.Notificar("Contraseña restablecida.");
        }, "No se pudo restablecer la contraseña.");
    }

    [RelayCommand]
    private async Task CambiarMiContrasenaAsync()
    {
        var actual = await _dialogos.PedirTextoAsync(
            "Cambiar mi contraseña", "Contraseña actual").ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(actual))
        {
            return;
        }

        var nueva = await _dialogos.PedirTextoAsync(
            "Cambiar mi contraseña",
            "Nueva contraseña (mínimo 6 caracteres, con letras y números)").ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(nueva))
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            await _autenticacion
                .CambiarContrasenaAsync(_sesion.UsuarioIdRequerido, actual, nueva)
                .ConfigureAwait(true);

            _dialogos.Notificar("Su contraseña se actualizó correctamente.");
        }, "No se pudo cambiar la contraseña.");
    }

    [RelayCommand]
    private async Task AlternarEstadoAsync()
    {
        if (UsuarioSeleccionado is null || !PuedeEditar)
        {
            return;
        }

        var activar = !UsuarioSeleccionado.Activo;

        await EjecutarAsync(async () =>
        {
            await _usuarios.CambiarEstadoAsync(UsuarioSeleccionado.Id, activar).ConfigureAwait(true);
            _dialogos.Notificar(activar ? "Usuario activado." : "Usuario desactivado.");
            await CargarUsuariosAsync().ConfigureAwait(true);
        }, "No se pudo cambiar el estado del usuario.");
    }

    [RelayCommand]
    private async Task EliminarAsync()
    {
        if (UsuarioSeleccionado is null || !PuedeEliminar)
        {
            return;
        }

        var confirmado = await _dialogos.ConfirmarAsync(
            "Eliminar usuario",
            $"¿Desea eliminar a «{UsuarioSeleccionado.NombreCompleto}»?\n\n" +
            "Si tiene movimientos registrados, se desactivará en lugar de borrarse.",
            "Eliminar", esDestructivo: true).ConfigureAwait(true);

        if (!confirmado)
        {
            return;
        }

        await EjecutarAsync(async () =>
        {
            await _usuarios.EliminarAsync(UsuarioSeleccionado.Id).ConfigureAwait(true);
            _dialogos.Notificar("Usuario eliminado.");
            await CargarUsuariosAsync().ConfigureAwait(true);
        }, "No se pudo eliminar el usuario.");
    }

    [RelayCommand]
    private Task GuardarPermisosAsync() => EjecutarAsync(async () =>
    {
        if (!PuedeEditar)
        {
            return;
        }

        var permisos = Permisos.Select(p => new PermisoRol
        {
            Rol = RolPermisos,
            Modulo = p.Modulo,
            PuedeVer = p.PuedeVer,
            PuedeCrear = p.PuedeCrear,
            PuedeEditar = p.PuedeEditar,
            PuedeEliminar = p.PuedeEliminar
        });

        await _usuarios.GuardarPermisosAsync(permisos).ConfigureAwait(true);

        _dialogos.Notificar(
            "Permisos guardados. Los usuarios de ese rol los verán aplicados en su próximo inicio de sesión.");
    }, "No se pudieron guardar los permisos.");
}
