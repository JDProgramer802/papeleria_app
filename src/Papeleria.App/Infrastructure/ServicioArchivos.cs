using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Papeleria.Data.Storage;
using Papeleria.Domain.Exceptions;

namespace Papeleria.App.Infrastructure;

/// <inheritdoc cref="IServicioArchivos" />
public class ServicioArchivos : IServicioArchivos
{
    public string? SeleccionarArchivo(string titulo, string filtro, string? carpetaInicial = null)
    {
        var dialogo = new OpenFileDialog
        {
            Title = titulo,
            Filter = filtro,
            CheckFileExists = true,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(carpetaInicial) && Directory.Exists(carpetaInicial))
        {
            dialogo.InitialDirectory = carpetaInicial;
        }

        return dialogo.ShowDialog() == true ? dialogo.FileName : null;
    }

    public string? SeleccionarDondeGuardar(
        string titulo, string filtro, string nombreSugerido, string? carpetaInicial = null)
    {
        var dialogo = new SaveFileDialog
        {
            Title = titulo,
            Filter = filtro,
            FileName = nombreSugerido,
            OverwritePrompt = true,
            AddExtension = true
        };

        var carpeta = string.IsNullOrWhiteSpace(carpetaInicial)
            ? RutasAplicacion.CarpetaExportaciones
            : carpetaInicial;

        if (Directory.Exists(carpeta))
        {
            dialogo.InitialDirectory = carpeta;
        }

        return dialogo.ShowDialog() == true ? dialogo.FileName : null;
    }

    public string? SeleccionarCarpeta(string titulo, string? carpetaInicial = null)
    {
        var dialogo = new OpenFolderDialog
        {
            Title = titulo,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(carpetaInicial) && Directory.Exists(carpetaInicial))
        {
            dialogo.InitialDirectory = carpetaInicial;
        }

        return dialogo.ShowDialog() == true ? dialogo.FolderName : null;
    }

    public async Task<string> GuardarImagenAsync(string rutaOrigen, string prefijo)
    {
        if (!File.Exists(rutaOrigen))
        {
            throw new NegocioException("La imagen seleccionada ya no existe.");
        }

        var informacion = new FileInfo(rutaOrigen);

        // Las imágenes se guardan dentro del almacén de la aplicación: así el respaldo
        // y el traslado del programa a otro equipo no dejan rutas rotas.
        const long limiteBytes = 8 * 1024 * 1024;

        if (informacion.Length > limiteBytes)
        {
            throw new NegocioException("La imagen supera el tamaño máximo permitido (8 MB).");
        }

        Directory.CreateDirectory(RutasAplicacion.CarpetaImagenes);

        var extension = Path.GetExtension(rutaOrigen);
        var nombre = $"{prefijo}_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..40] + extension;
        var destino = Path.Combine(RutasAplicacion.CarpetaImagenes, nombre);

        await using (var origen = File.OpenRead(rutaOrigen))
        await using (var copia = File.Create(destino))
        {
            await origen.CopyToAsync(copia).ConfigureAwait(false);
        }

        return destino;
    }

    public void AbrirConAplicacionPredeterminada(string ruta)
    {
        if (!File.Exists(ruta))
        {
            throw new NegocioException("El archivo generado ya no está disponible.");
        }

        Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
    }

    public void AbrirCarpeta(string ruta)
    {
        var carpeta = Directory.Exists(ruta) ? ruta : Path.GetDirectoryName(ruta);

        if (string.IsNullOrWhiteSpace(carpeta) || !Directory.Exists(carpeta))
        {
            throw new NegocioException("La carpeta indicada no existe.");
        }

        Process.Start(new ProcessStartInfo(carpeta) { UseShellExecute = true });
    }
}
