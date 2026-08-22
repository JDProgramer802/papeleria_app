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

    /// <summary>Cupo de crédito. En cero el cliente paga siempre de contado.</summary>
    [ObservableProperty] private decimal _limiteCredito;

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

/// <summary>Formulario para recibir un abono de un cliente con deuda.</summary>
public partial class AbonoDialogoVistaModelo : DialogoFormularioBase
{
    public AbonoDialogoVistaModelo(
        IServicioDialogos dialogos, Func<Task> guardar, string cliente, decimal saldo)
        : base(dialogos, guardar)
    {
        Titulo = "Registrar abono";
        Cliente = cliente;
        Saldo = saldo;
        Monto = saldo;

        MetodosPago = new ObservableCollection<OpcionEnum<MetodoPago>>(
            Enumeraciones.Opciones<MetodoPago>()
                .Where(o => o.Valor is MetodoPago.Efectivo or MetodoPago.Tarjeta or MetodoPago.Transferencia));
    }

    public string Cliente { get; }

    /// <summary>Deuda vigente; el abono no puede superarla.</summary>
    public decimal Saldo { get; }

    public ObservableCollection<OpcionEnum<MetodoPago>> MetodosPago { get; }

    [ObservableProperty] private decimal _monto;
    [ObservableProperty] private MetodoPago _metodoPago = MetodoPago.Efectivo;
    [ObservableProperty] private string? _observaciones;

    public string SaldoTexto => Formatos.Moneda(Saldo);

    /// <summary>Lo que quedaría debiendo tras aplicar el abono que se está escribiendo.</summary>
    public string RestanteTexto => Formatos.Moneda(Math.Max(Saldo - Monto, 0));

    partial void OnMontoChanged(decimal value) => OnPropertyChanged(nameof(RestanteTexto));
}
