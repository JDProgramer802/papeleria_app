namespace Papeleria.App.Infrastructure;

/// <summary>La implementan las páginas que aceptan un parámetro al navegar hacia ellas.</summary>
public interface IRecibeParametro
{
    Task RecibirParametroAsync(object parametro);
}

/// <summary>
/// Parámetro de navegación hacia Compras: abre el formulario de registro con el
/// producto ya cargado, para poder comprar directamente desde el catálogo.
/// </summary>
public record CompraDeProducto(int ProductoId, string Codigo, string Nombre);

/// <summary>
/// Navegación centrada en el modelo de vista: se navega indicando la clave del módulo
/// y el contenedor resuelve el modelo correspondiente; la vista la elige un DataTemplate.
/// </summary>
public interface INavegacion
{
    PaginaVistaModelo? PaginaActual { get; }

    event EventHandler<PaginaVistaModelo>? Navegado;

    /// <summary>Comprueba permisos y existencia del módulo antes de intentar navegar.</summary>
    bool PuedeNavegar(string modulo);

    Task NavegarAsync(string modulo, object? parametro = null);

    /// <summary>Vuelve a cargar la página activa.</summary>
    Task RecargarAsync();
}
