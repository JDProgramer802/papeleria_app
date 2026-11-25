using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Papeleria.App.Infrastructure;
using Papeleria.App.ViewModels.Paginas;
using Papeleria.Business.Dtos;
using Papeleria.Business.Security;
using Papeleria.Business.Services;
using Papeleria.Domain.Enums;

namespace Papeleria.Pruebas;

public static partial class Programa
{
    private static void ComprobarGastosVisuales(IServiceProvider proveedor)
    {
        using var cola = new SnackbarMessageQueue();
        var vm = new GastosVistaModelo(proveedor.GetRequiredService<IServicioGastos>(),
            new ServicioDialogos(cola), new ServicioArchivos(),
            proveedor.GetRequiredService<IServicioExportacion>(), proveedor.GetRequiredService<IContextoSesion>());
        vm.Gastos.Add(new GastoDto(1, DateTime.Today, "Servicio de internet", "Servicios públicos", 89000,
            MetodoPago.Transferencia, "Proveedor de internet", "REC-001", false, "Administrador", null));
        vm.Gastos.Add(new GastoDto(2, DateTime.Today, "Transporte de pedidos", "Transporte", 25000,
            MetodoPago.Efectivo, "Mensajería", "REC-002", false, "Administrador", null));
        vm.Total = 114000; vm.Efectivo = 25000; vm.OtrosPagos = 89000;
        vm.Resumen = "2 gastos vigentes · 0 anulados visibles";
        var muestras = Environment.GetEnvironmentVariable("PAPELSOFT_MUESTRAS");
        Application.Current.Resources.MergedDictionaries.Insert(0, new CustomColorTheme
        {
            BaseTheme = BaseTheme.Light, PrimaryColor = Color.FromRgb(3, 121, 238), SecondaryColor = Colors.Teal
        });
        var paleta = new PaletteHelper();
        var tema = paleta.GetTheme();
        foreach (var oscuro in new[] { false, true })
        foreach (var formulario in new[] { false, true })
        {
            tema.SetBaseTheme(oscuro ? BaseTheme.Dark : BaseTheme.Light);
            paleta.SetTheme(tema);
            vm.FormularioAbierto = formulario;
            var vista = new App.Views.Paginas.GastosView { DataContext = vm };
            var superficie = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(oscuro ? "#151B27" : "#F3F5F9")),
                Child = vista
            };
            System.Windows.Documents.TextElement.SetForeground(superficie, oscuro ? Brushes.WhiteSmoke : Brushes.Black);
            var ancho = formulario ? 900 : 1180;
            superficie.Measure(new Size(ancho, 760));
            superficie.Arrange(new Rect(0, 0, ancho, 760));
            superficie.UpdateLayout();
            Verificar($"gastos se dibuja en tema {(oscuro ? "oscuro" : "claro")} ({(formulario ? "formulario" : "listado")})", vista.ActualWidth == ancho);
            if (!string.IsNullOrWhiteSpace(muestras))
            {
                Directory.CreateDirectory(muestras);
                var mapa = new RenderTargetBitmap(ancho, 760, 96, 96, PixelFormats.Pbgra32);
                mapa.Render(superficie);
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(mapa));
                using var archivo = File.Create(Path.Combine(muestras, $"gastos-{(oscuro ? "oscuro" : "claro")}-{(formulario ? "formulario" : "listado")}.png"));
                png.Save(archivo);
            }
        }
        tema.SetBaseTheme(BaseTheme.Light);
        paleta.SetTheme(tema);
    }
}
