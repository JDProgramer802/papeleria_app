namespace Papeleria.App.Infrastructure;

/// <summary>
/// Diálogos y avisos de la aplicación. Se expone como servicio para que los modelos
/// de vista no dependan de tipos de WPF y sigan siendo comprobables.
/// </summary>
public interface IServicioDialogos
{
    /// <summary>Pregunta de sí/no. Devuelve <c>true</c> si el usuario confirma.</summary>
    Task<bool> ConfirmarAsync(
        string titulo,
        string mensaje,
        string textoAceptar = "Aceptar",
        string textoCancelar = "Cancelar",
        bool esDestructivo = false);

    /// <summary>Aviso informativo con un único botón.</summary>
    Task InformarAsync(string titulo, string mensaje, bool esError = false);

    /// <summary>Pide un texto al usuario; devuelve <c>null</c> si cancela.</summary>
    Task<string?> PedirTextoAsync(
        string titulo,
        string etiqueta,
        string? valorInicial = null,
        bool multilinea = false,
        bool obligatorio = true);

    /// <summary>Muestra un diálogo personalizado a partir de su modelo de vista.</summary>
    Task<object?> MostrarAsync(object modeloVista);

    /// <summary>Cierra el diálogo activo devolviendo el resultado indicado.</summary>
    void Cerrar(object? resultado = null);

    /// <summary>Mensaje breve en la barra inferior.</summary>
    void Notificar(string mensaje);
}

/// <summary>Diálogos del sistema para abrir y guardar archivos.</summary>
public interface IServicioArchivos
{
    string? SeleccionarArchivo(string titulo, string filtro, string? carpetaInicial = null);

    string? SeleccionarDondeGuardar(string titulo, string filtro, string nombreSugerido,
        string? carpetaInicial = null);

    string? SeleccionarCarpeta(string titulo, string? carpetaInicial = null);

    /// <summary>Copia una imagen al almacén local y devuelve su nueva ruta.</summary>
    Task<string> GuardarImagenAsync(string rutaOrigen, string prefijo);

    /// <summary>Abre un archivo con la aplicación predeterminada del sistema.</summary>
    void AbrirConAplicacionPredeterminada(string ruta);

    void AbrirCarpeta(string ruta);
}
