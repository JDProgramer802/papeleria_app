using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;

namespace Papeleria.App.ViewModels.Dialogos;

/// <summary>Diálogo genérico de confirmación o aviso.</summary>
public partial class DialogoMensajeVistaModelo : ObservableObject
{
    [ObservableProperty]
    private string _titulo = string.Empty;

    [ObservableProperty]
    private string _mensaje = string.Empty;

    [ObservableProperty]
    private string _textoAceptar = "Aceptar";

    [ObservableProperty]
    private string _textoCancelar = "Cancelar";

    [ObservableProperty]
    private bool _mostrarCancelar = true;

    /// <summary>Tiñe el diálogo en rojo cuando la acción no se puede deshacer.</summary>
    [ObservableProperty]
    private bool _esDestructivo;

    [ObservableProperty]
    private PackIconKind _icono = PackIconKind.HelpCircleOutline;
}

/// <summary>Diálogo que pide un texto (motivos de anulación, conceptos de caja…).</summary>
public partial class DialogoTextoVistaModelo : ObservableObject
{
    [ObservableProperty]
    private string _titulo = string.Empty;

    [ObservableProperty]
    private string _etiqueta = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeAceptar))]
    private string _valor = string.Empty;

    [ObservableProperty]
    private bool _multilinea;

    [ObservableProperty]
    private bool _obligatorio = true;

    /// <summary>Impide aceptar mientras el campo obligatorio esté vacío.</summary>
    public bool PuedeAceptar => !Obligatorio || !string.IsNullOrWhiteSpace(Valor);
}
