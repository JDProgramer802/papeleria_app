using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Papeleria.Business.Services;
using Papeleria.Domain.Constants;

namespace Papeleria.App.Infrastructure;

/// <summary>Cambia entre el tema claro y el oscuro, y recuerda la preferencia del usuario.</summary>
public interface IServicioTema
{
    bool EsOscuro { get; }

    event EventHandler? TemaCambiado;

    /// <summary>Aplica el tema guardado en la configuración. Se llama al arrancar.</summary>
    void AplicarTemaGuardado();

    Task EstablecerAsync(bool oscuro);

    Task AlternarAsync();
}

/// <inheritdoc cref="IServicioTema" />
public class ServicioTema : IServicioTema
{
    private readonly IServicioConfiguracion _configuracion;
    private readonly PaletteHelper _paleta = new();

    public ServicioTema(IServicioConfiguracion configuracion) => _configuracion = configuracion;

    public bool EsOscuro { get; private set; }

    public event EventHandler? TemaCambiado;

    public void AplicarTemaGuardado() =>
        Aplicar(_configuracion.ObtenerBooleano(ClavesConfiguracion.TemaOscuro));

    public async Task EstablecerAsync(bool oscuro)
    {
        if (EsOscuro == oscuro)
        {
            return;
        }

        Aplicar(oscuro);

        await _configuracion.GuardarAsync(ClavesConfiguracion.TemaOscuro, oscuro.ToString())
            .ConfigureAwait(true);
    }

    public Task AlternarAsync() => EstablecerAsync(!EsOscuro);

    private void Aplicar(bool oscuro)
    {
        var tema = _paleta.GetTheme();
        tema.SetBaseTheme(oscuro ? BaseTheme.Dark : BaseTheme.Light);

        // El color primario se toma de la configuración para que la papelería pueda
        // ajustar la interfaz a su identidad visual.
        var colorPrimario = LeerColorConfigurado();
        tema.SetPrimaryColor(colorPrimario);
        tema.SetSecondaryColor(ColorSecundario);

        _paleta.SetTheme(tema);

        EsOscuro = oscuro;
        TemaCambiado?.Invoke(this, EventArgs.Empty);
    }

    private static Color ColorSecundario => (Color)ColorConverter.ConvertFromString("#FF8F00");

    private Color LeerColorConfigurado()
    {
        var texto = _configuracion.ObtenerTexto(ClavesConfiguracion.ColorPrimario, "#1565C0");

        try
        {
            return (Color)ColorConverter.ConvertFromString(texto);
        }
        catch (FormatException)
        {
            return (Color)ColorConverter.ConvertFromString("#1565C0");
        }
    }
}
