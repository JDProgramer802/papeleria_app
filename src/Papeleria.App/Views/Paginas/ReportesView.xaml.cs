using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Papeleria.App.ViewModels.Paginas;

namespace Papeleria.App.Views.Paginas;

/// <summary>Vista del centro de informes.</summary>
public partial class ReportesView : UserControl
{
    public ReportesView() => InitializeComponent();

    /// <summary>
    /// Arregla las columnas que la grilla genera sola a partir del informe.
    ///
    /// Dos cosas que no salían bien. La primera es un fallo: si el título de una columna
    /// lleva un punto —«Ticket prom.», «Dif. caja»—, WPF lee ese punto como un salto de
    /// propiedad y la columna aparece VACÍA, con su encabezado y sin un solo dato. La
    /// forma con corchetes trata el nombre como una sola clave, tenga los caracteres que
    /// tenga. La segunda es de presentación: los importes y los conteos se alineaban a la
    /// izquierda, así que las cifras no se podían comparar de un vistazo entre filas.
    /// </summary>
    private void AlGenerarColumna(object remitente, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.Column is not DataGridBoundColumn columna)
        {
            return;
        }

        columna.Binding = new Binding($"[{e.PropertyName}]");

        var definicion = (DataContext as ReportesVistaModelo)?.Reporte?.Columnas
            .FirstOrDefault(c => c.Titulo == e.PropertyName);

        if (definicion is { AlinearDerecha: true })
        {
            columna.ElementStyle = (Style)FindResource("CeldaNumerica");
            columna.HeaderStyle = (Style)FindResource("EncabezadoNumerico");
        }
    }
}
