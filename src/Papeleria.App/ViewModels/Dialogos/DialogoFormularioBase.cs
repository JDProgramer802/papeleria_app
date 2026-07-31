using CommunityToolkit.Mvvm.Input;
using Papeleria.App.Infrastructure;

namespace Papeleria.App.ViewModels.Dialogos;

/// <summary>
/// Base de los formularios en diálogo. El guardado lo aporta la página que abre el
/// formulario, de modo que los servicios de negocio permanecen en el modelo de vista
/// de la página y aquí solo queda la presentación y el manejo de errores.
/// </summary>
public abstract partial class DialogoFormularioBase : VistaModeloBase
{
    private readonly IServicioDialogos _dialogos;
    private readonly Func<Task> _guardar;

    protected DialogoFormularioBase(IServicioDialogos dialogos, Func<Task> guardar)
    {
        _dialogos = dialogos;
        _guardar = guardar;
    }

    [RelayCommand]
    private Task GuardarAsync() => EjecutarAsync(async () =>
    {
        await _guardar().ConfigureAwait(true);
        _dialogos.Cerrar(true);
    }, "No se pudo guardar la información.");

    [RelayCommand]
    private void Cancelar() => _dialogos.Cerrar(false);
}
