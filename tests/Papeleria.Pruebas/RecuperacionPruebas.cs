using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Papeleria.Business.Security;
using Papeleria.Business.Services;
using Papeleria.Data.Seed;
using Papeleria.Domain.Exceptions;

namespace Papeleria.Pruebas;

public static partial class Programa
{
    /// <summary>
    /// El código de recuperación es una segunda llave de la puerta: tiene que abrir
    /// con el código correcto, no abrir con ningún otro, gastarse al usarlo y no
    /// dejarse adivinar a punta de intentos.
    ///
    /// Va de último porque le cambia la contraseña al administrador.
    /// </summary>
    private static async Task ComprobarRecuperacionAsync(IServiceProvider proveedor)
    {
        Console.WriteLine();
        Console.WriteLine("Recuperación de contraseña");
        Console.WriteLine("──────────────────────────");

        var recuperacion = proveedor.GetRequiredService<IServicioRecuperacionAcceso>();
        var autenticacion = proveedor.GetRequiredService<IServicioAutenticacion>();
        var sesion = proveedor.GetRequiredService<IContextoSesion>();
        var admin = SembradorDatos.UsuarioAdministrador;

        async Task<string?> Rechazo(Func<Task> operacion)
        {
            try
            {
                await operacion().ConfigureAwait(false);
                return null;
            }
            catch (NegocioException ex)
            {
                return ex.Message;
            }
        }

        Verificar("de fábrica nadie tiene código", recuperacion.FechaCodigo(sesion.UsuarioIdRequerido) is null);

        var codigo = await recuperacion.GenerarCodigoAsync().ConfigureAwait(false);

        Verificar("el código tiene la forma XXXX-XXXX-XXXX-XXXX",
            System.Text.RegularExpressions.Regex.IsMatch(codigo, "^[A-Z2-9]{4}(-[A-Z2-9]{4}){3}$"), codigo);
        Verificar("queda registrado cuándo se generó", recuperacion.FechaCodigo(sesion.UsuarioIdRequerido) is not null);

        var otro = await recuperacion.GenerarCodigoAsync().ConfigureAwait(false);
        Verificar("dos códigos seguidos no se repiten", otro != codigo);

        var mensajeViejo = await Rechazo(() =>
            recuperacion.RestablecerConCodigoAsync(admin, codigo, "Nueva123")).ConfigureAwait(false);
        Verificar("generar otro invalida el anterior", mensajeViejo is not null, mensajeViejo);

        var mensajeDesconocido = await Rechazo(() =>
            recuperacion.RestablecerConCodigoAsync("nadie", otro, "Nueva123")).ConfigureAwait(false);
        Verificar("un usuario que no existe recibe el mismo mensaje que un código malo",
            mensajeDesconocido == mensajeViejo, mensajeDesconocido);

        var mensajeDebil = await Rechazo(() =>
            recuperacion.RestablecerConCodigoAsync(admin, otro, "corta")).ConfigureAwait(false);
        Verificar("la contraseña nueva cumple las mismas reglas de siempre",
            mensajeDebil is not null && mensajeDebil.Contains("6 caracteres"), mensajeDebil);

        // Escrito como lo copiaría alguien de un papel: en minúscula y sin guiones.
        var tecleado = otro.Replace("-", " ").ToLowerInvariant();
        var siguiente = await recuperacion.RestablecerConCodigoAsync(admin, tecleado, "Nueva123")
            .ConfigureAwait(false);

        Verificar("con el código correcto se restablece, aunque se escriba en minúscula y sin guiones",
            await Rechazo(() => autenticacion.AutenticarAsync(admin, "Nueva123")).ConfigureAwait(false) is null);

        Verificar("la contraseña anterior deja de servir",
            await Rechazo(() => autenticacion.AutenticarAsync(
                admin, SembradorDatos.ContrasenaAdministradorPorDefecto)).ConfigureAwait(false) is not null);

        Verificar("el código usado se gasta", await Rechazo(() =>
            recuperacion.RestablecerConCodigoAsync(admin, otro, "Otra1234")).ConfigureAwait(false) is not null);

        Verificar("y a cambio se entrega uno nuevo", siguiente != otro && siguiente.Length == 19);

        // ── Fuerza bruta ────────────────────────────────────────────────────
        // El último intento ya fue un fallo (el código gastado); con cuatro más se bloquea.
        for (var i = 0; i < 4; i++)
        {
            await Rechazo(() => recuperacion.RestablecerConCodigoAsync(admin, "AAAA-AAAA-AAAA-AAAA", "Otra1234"))
                .ConfigureAwait(false);
        }

        var bloqueado = await Rechazo(() =>
            recuperacion.RestablecerConCodigoAsync(admin, siguiente, "Otra1234")).ConfigureAwait(false);

        Verificar("tras cinco fallos ni el código correcto entra por un rato",
            bloqueado is not null && bloqueado.Contains("Demasiados intentos"), bloqueado);
    }

    /// <summary>
    /// Con PAPELSOFT_MUESTRAS, deja en esa carpeta los dos pasos de la recuperación en PNG.
    /// Se llama junto a las demás pantallas: una ventana solo se dibuja en el hilo de interfaz.
    /// </summary>
    private static void DibujarRecuperacion(IServiceProvider proveedor)
    {
        var carpeta = Environment.GetEnvironmentVariable("PAPELSOFT_MUESTRAS");

        if (string.IsNullOrWhiteSpace(carpeta))
        {
            return;
        }

        Directory.CreateDirectory(carpeta);

        var modelo = ActivatorUtilities.CreateInstance<App.ViewModels.LoginVistaModelo>(proveedor);
        var ventana = new App.Views.LoginWindow
        {
            DataContext = modelo,
            Left = -20000,
            ShowActivated = false
        };

        ventana.Show();

        void Guardar(string nombre)
        {
            ventana.UpdateLayout();

            var mapa = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)ventana.ActualWidth, (int)ventana.ActualHeight, 96, 96,
                System.Windows.Media.PixelFormats.Pbgra32);
            mapa.Render((System.Windows.Media.Visual)ventana.Content);

            var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
            png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(mapa));

            using var archivo = File.Create(Path.Combine(carpeta, nombre));
            png.Save(archivo);
        }

        modelo.NombreUsuario = SembradorDatos.UsuarioAdministrador;
        Guardar("acceso.png");

        modelo.IniciarRecuperacionCommand.Execute(null);
        modelo.CodigoRecuperacion = "K7QX-M2PA-9DTR-4HWE";
        modelo.MensajeError = "El usuario o el código de recuperación no son correctos.";
        Guardar("recuperacion.png");

        modelo.MensajeError = null;
        modelo.CodigoNuevo = "R4NB-8XKC-WT2M-HQ7S";
        Guardar("recuperacion-codigo-nuevo.png");

        ventana.Close();
    }
}
