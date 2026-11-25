using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Papeleria.App.ViewModels.Dialogos;

/// <summary>
/// Muestra el código de recuperación recién generado. Es la única vez que se ve: en
/// la base de datos solo queda su hash, igual que con las contraseñas.
/// </summary>
public partial class CodigoRecuperacionDialogoVistaModelo : ObservableObject
{
    public CodigoRecuperacionDialogoVistaModelo(string codigo, string nombreUsuario)
    {
        Codigo = codigo;
        NombreUsuario = nombreUsuario;
    }

    public string Codigo { get; }

    public string NombreUsuario { get; }

    [ObservableProperty]
    private bool _copiado;

    [RelayCommand]
    private void Copiar()
    {
        try
        {
            System.Windows.Clipboard.SetText(Codigo);
            Copiado = true;
        }
        catch (Exception)
        {
            // Otro programa puede tener el portapapeles tomado; el código sigue en pantalla.
        }
    }
}
