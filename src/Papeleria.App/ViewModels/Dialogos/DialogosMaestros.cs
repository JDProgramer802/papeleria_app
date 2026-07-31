using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Papeleria.App.Infrastructure;
using Papeleria.Business.Common;
using Papeleria.Domain.Enums;

namespace Papeleria.App.ViewModels.Dialogos;

/// <summary>Formulario de categorías, marcas y unidades de medida.</summary>
public partial class CatalogoDialogoVistaModelo : DialogoFormularioBase
{
    public CatalogoDialogoVistaModelo(
        IServicioDialogos dialogos, Func<Task> guardar, string titulo, bool usaAbreviatura)
        : base(dialogos, guardar)
    {
        Titulo = titulo;
        UsaAbreviatura = usaAbreviatura;
    }

    /// <summary>Solo las unidades de medida piden abreviatura.</summary>
    public bool UsaAbreviatura { get; }

    [ObservableProperty] private string _nombre = string.Empty;
    [ObservableProperty] private string _abreviatura = string.Empty;
    [ObservableProperty] private string? _descripcion;
    [ObservableProperty] private bool _activo = true;
}

/// <summary>Formulario de proveedores.</summary>
public partial class ProveedorDialogoVistaModelo : DialogoFormularioBase
{
    public ProveedorDialogoVistaModelo(IServicioDialogos dialogos, Func<Task> guardar, string titulo)
        : base(dialogos, guardar) => Titulo = titulo;

    [ObservableProperty] private string _nombre = string.Empty;
    [ObservableProperty] private string? _nit;
    [ObservableProperty] private string? _contacto;
    [ObservableProperty] private string? _telefono;
    [ObservableProperty] private string? _correo;
    [ObservableProperty] private string? _direccion;
    [ObservableProperty] private string? _ciudad;
    [ObservableProperty] private string? _observaciones;
    [ObservableProperty] private bool _activo = true;
}

/// <summary>Formulario de clientes.</summary>
public partial class ClienteDialogoVistaModelo : DialogoFormularioBase
{
    public ClienteDialogoVistaModelo(IServicioDialogos dialogos, Func<Task> guardar, string titulo)
        : base(dialogos, guardar)
    {
        Titulo = titulo;
        TiposDocumento = new ObservableCollection<OpcionEnum<TipoDocumento>>(
            Enumeraciones.Opciones<TipoDocumento>());
    }

    public ObservableCollection<OpcionEnum<TipoDocumento>> TiposDocumento { get; }

    [ObservableProperty] private string _nombre = string.Empty;
    [ObservableProperty] private TipoDocumento _tipoDocumento = TipoDocumento.CedulaCiudadania;
    [ObservableProperty] private string? _numeroDocumento;
    [ObservableProperty] private string? _telefono;
    [ObservableProperty] private string? _correo;
    [ObservableProperty] private string? _direccion;
    [ObservableProperty] private string? _ciudad;
    [ObservableProperty] private string? _observaciones;
    [ObservableProperty] private bool _activo = true;

    /// <summary>El «Consumidor final» no puede desactivarse ni eliminarse.</summary>
    [ObservableProperty] private bool _esProtegido;
}

/// <summary>Formulario de usuarios del sistema.</summary>
public partial class UsuarioDialogoVistaModelo : DialogoFormularioBase
{
    public UsuarioDialogoVistaModelo(
        IServicioDialogos dialogos, Func<Task> guardar, string titulo, bool esNuevo)
        : base(dialogos, guardar)
    {
        Titulo = titulo;
        EsNuevo = esNuevo;
        Roles = new ObservableCollection<OpcionEnum<RolUsuario>>(Enumeraciones.Opciones<RolUsuario>());
    }

    public bool EsNuevo { get; }

    public ObservableCollection<OpcionEnum<RolUsuario>> Roles { get; }

    [ObservableProperty] private string _nombreUsuario = string.Empty;
    [ObservableProperty] private string _nombreCompleto = string.Empty;
    [ObservableProperty] private string? _correo;
    [ObservableProperty] private string? _telefono;
    [ObservableProperty] private RolUsuario _rol = RolUsuario.Cajero;
    [ObservableProperty] private bool _activo = true;
    [ObservableProperty] private bool _esProtegido;

    /// <summary>Solo se pide al crear; para cambiarla después existe «Restablecer contraseña».</summary>
    [ObservableProperty] private string _contrasena = string.Empty;

    [ObservableProperty] private string _confirmacionContrasena = string.Empty;
}
