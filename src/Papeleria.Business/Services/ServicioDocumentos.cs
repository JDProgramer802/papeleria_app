using Papeleria.Business.Common;
using Papeleria.Business.Dtos;
using Papeleria.Data.Storage;
using Papeleria.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Papeleria.Business.Services;

/// <inheritdoc cref="IServicioDocumentos" />
public class ServicioDocumentos : IServicioDocumentos
{
    private const string ColorPrincipal = "#1565C0";

    private readonly IServicioConfiguracion _configuracion;
    private readonly IServicioCodigoBarras _codigoBarras;

    static ServicioDocumentos()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ServicioDocumentos(IServicioConfiguracion configuracion, IServicioCodigoBarras codigoBarras)
    {
        _configuracion = configuracion;
        _codigoBarras = codigoBarras;
    }

    // ── Factura de venta ────────────────────────────────────────────────────

    public async Task<string> GenerarFacturaAsync(
        VentaDetalladaDto venta,
        FormatoFactura formato = FormatoFactura.Recibo80mm,
        string? rutaDestino = null,
        bool esCopia = false,
        CancellationToken ct = default)
    {
        var ruta = rutaDestino ?? RutasAplicacion.RutaTemporal(".pdf");
        var recibo = new ReciboVenta(venta, _configuracion.ObtenerEmpresa(), esCopia);

        await Task.Run(() =>
        {
            var documento = formato == FormatoFactura.Recibo80mm
                ? ConstruirRecibo(recibo)
                : ConstruirFacturaCarta(recibo);

            documento.GeneratePdf(ruta);
        }, ct).ConfigureAwait(false);

        return ruta;
    }

    /// <summary>
    /// Tirilla continua de 80 mm: el alto crece con el número de líneas. Es el mismo
    /// diseño que sale por la impresora térmica, renglón por renglón.
    /// </summary>
    private IDocument ConstruirRecibo(ReciboVenta recibo) =>
        Document.Create(documento =>
        {
            var venta = recibo.Venta;
            var empresa = recibo.Empresa;

            documento.Page(pagina =>
            {
                pagina.ContinuousSize(80, Unit.Millimetre);
                pagina.MarginHorizontal(4, Unit.Millimetre);
                pagina.MarginVertical(5, Unit.Millimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(7.5f).FontFamily(Fonts.Consolas));

                pagina.Content().Column(columna =>
                {
                    // ── Empresa ─────────────────────────────────────────────
                    if (empresa.TieneLogo)
                    {
                        columna.Item().AlignCenter().Height(16, Unit.Millimetre)
                            .Image(empresa.LogoPath).FitHeight();
                        columna.Item().Height(2, Unit.Millimetre);
                    }

                    columna.Item().AlignCenter().Text(empresa.Nombre.ToUpper(Formatos.Cultura))
                        .Bold().FontSize(10.5f);

                    if (!string.IsNullOrWhiteSpace(empresa.Eslogan))
                    {
                        columna.Item().AlignCenter().Text(empresa.Eslogan).Italic().FontSize(7);
                    }

                    foreach (var linea in recibo.Encabezado)
                    {
                        columna.Item().AlignCenter().Text(linea).FontSize(7);
                    }

                    SeparadorRecibo(columna, doble: true);

                    // ── Documento ───────────────────────────────────────────
                    columna.Item().AlignCenter().Text(recibo.Titulo).Bold().FontSize(9);
                    columna.Item().AlignCenter().Text(recibo.Numero).Bold().FontSize(11);

                    if (recibo.EsCopia)
                    {
                        columna.Item().PaddingTop(2).AlignCenter().Border(0.75f).PaddingHorizontal(6)
                            .Text("COPIA").Bold().FontSize(8);
                    }

                    if (recibo.EsAnulada)
                    {
                        columna.Item().PaddingTop(3).Background(Colors.Black).PaddingVertical(2).AlignCenter()
                            .Text("*** DOCUMENTO ANULADO ***").Bold().FontColor(Colors.White);

                        if (!string.IsNullOrWhiteSpace(recibo.MotivoAnulacion))
                        {
                            columna.Item().AlignCenter().Text($"Motivo: {recibo.MotivoAnulacion}").FontSize(7);
                        }
                    }

                    SeparadorRecibo(columna);

                    foreach (var fila in recibo.DatosVenta)
                    {
                        FilaDatoRecibo(columna, fila);
                    }

                    SeparadorRecibo(columna);

                    // ── Renglones ───────────────────────────────────────────
                    columna.Item().Row(fila =>
                    {
                        fila.RelativeItem().Text("DESCRIPCIÓN / CANT. x V.UNIT").Bold().FontSize(7);
                        fila.AutoItem().Text("VALOR").Bold().FontSize(7);
                        fila.ConstantItem(8);
                    });

                    SeparadorRecibo(columna);

                    foreach (var linea in recibo.Lineas)
                    {
                        columna.Item().Text(linea.Descripcion).Bold();

                        columna.Item().Row(fila =>
                        {
                            fila.RelativeItem().Text($"  {linea.CantidadPorPrecio}");
                            fila.AutoItem().Text(linea.Valor);
                            fila.ConstantItem(8).AlignRight().Text(linea.MarcaIva);
                        });

                        if (linea.TieneDescuento)
                        {
                            columna.Item().Row(fila =>
                            {
                                fila.RelativeItem().Text(linea.Descuento!);
                                fila.AutoItem().Text(linea.ValorDescuento!);
                                fila.ConstantItem(8);
                            });
                        }

                        columna.Item().Height(1.5f);
                    }

                    SeparadorRecibo(columna);

                    // ── Totales ─────────────────────────────────────────────
                    foreach (var fila in recibo.Totales)
                    {
                        FilaValorRecibo(columna, fila);
                    }

                    columna.Item().PaddingTop(3).Background(Colors.Black)
                        .PaddingVertical(3).PaddingHorizontal(4).Row(fila =>
                        {
                            fila.RelativeItem().Text("TOTAL A PAGAR").Bold().FontSize(10.5f).FontColor(Colors.White);
                            fila.AutoItem().Text(recibo.TotalTexto).Bold().FontSize(10.5f).FontColor(Colors.White);
                        });

                    columna.Item().PaddingTop(3).Text(recibo.TotalEnLetras).FontSize(6.5f);

                    // ── Discriminación del IVA ──────────────────────────────
                    if (recibo.DiscriminaIva)
                    {
                        SeparadorRecibo(columna);

                        columna.Item().Row(fila =>
                        {
                            fila.ConstantItem(10);
                            fila.RelativeItem(3).Text("TARIFA").Bold().FontSize(7);
                            fila.RelativeItem(3).AlignRight().Text("BASE").Bold().FontSize(7);
                            fila.RelativeItem(3).AlignRight().Text("IVA").Bold().FontSize(7);
                        });

                        foreach (var impuesto in recibo.Impuestos)
                        {
                            columna.Item().Row(fila =>
                            {
                                fila.ConstantItem(10).Text(impuesto.Marca).FontSize(7);
                                fila.RelativeItem(3).Text(impuesto.Tarifa).FontSize(7);
                                fila.RelativeItem(3).AlignRight().Text(impuesto.Base).FontSize(7);
                                fila.RelativeItem(3).AlignRight().Text(impuesto.Iva).FontSize(7);
                            });
                        }
                    }

                    SeparadorRecibo(columna);

                    // ── Pago ────────────────────────────────────────────────
                    foreach (var fila in recibo.Pago)
                    {
                        FilaValorRecibo(columna, fila);
                    }

                    columna.Item().PaddingTop(4).AlignCenter().Text(recibo.ResumenArticulos).FontSize(7);

                    // ── Resolución y pie ────────────────────────────────────
                    if (recibo.Resolucion.Count > 0)
                    {
                        SeparadorRecibo(columna);

                        foreach (var linea in recibo.Resolucion)
                        {
                            columna.Item().AlignCenter().Text(linea).FontSize(6.5f);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(recibo.Pie))
                    {
                        columna.Item().PaddingTop(5).AlignCenter().Text(recibo.Pie).FontSize(8).Bold();
                    }

                    // El código del documento permite recuperar la venta escaneando la tirilla.
                    var imagen = _codigoBarras.GenerarPng(venta.NumeroFactura,
                        SimbologiaCodigoBarras.Code128, 420, 90);

                    columna.Item().PaddingTop(6).AlignCenter().Width(56, Unit.Millimetre).Image(imagen);
                    columna.Item().AlignCenter().Text(venta.NumeroFactura).FontSize(7);

                    if (recibo.EsCopia)
                    {
                        columna.Item().PaddingTop(3).AlignCenter().Text(recibo.LineaCopia).FontSize(6.5f);
                    }

                    columna.Item().PaddingTop(3).AlignCenter().Text(recibo.LineaSoftware).FontSize(6);
                });
            });
        });

    /// <summary>Raya de lado a lado; la doble separa el encabezado de la empresa.</summary>
    private static void SeparadorRecibo(ColumnDescriptor columna, bool doble = false) =>
        columna.Item().PaddingVertical(3).Column(raya =>
        {
            raya.Item().LineHorizontal(0.6f);

            if (doble)
            {
                raya.Item().Height(1.2f);
                raya.Item().LineHorizontal(0.6f);
            }
        });

    private static void FilaDatoRecibo(ColumnDescriptor columna, FilaRecibo fila) =>
        columna.Item().Row(renglon =>
        {
            renglon.ConstantItem(17, Unit.Millimetre).Text($"{fila.Etiqueta}:");
            renglon.RelativeItem().Text(fila.Valor);
        });

    private static void FilaValorRecibo(ColumnDescriptor columna, FilaRecibo fila) =>
        columna.Item().Row(renglon =>
        {
            renglon.RelativeItem().Text(fila.Etiqueta);
            renglon.AutoItem().Text(fila.Valor);
        });

    /// <summary>Formato carta, con encabezado de empresa y tabla de detalle.</summary>
    private IDocument ConstruirFacturaCarta(ReciboVenta recibo) =>
        Document.Create(documento =>
        {
            var venta = recibo.Venta;
            var empresa = recibo.Empresa;

            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.Letter);
                pagina.Margin(28);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(9).FontFamily(Fonts.Calibri));

                pagina.Header().Column(encabezado =>
                {
                    encabezado.Item().Row(fila =>
                    {
                        fila.RelativeItem().Column(datos =>
                        {
                            datos.Item().Text(empresa.Nombre).FontSize(16).Bold().FontColor(ColorPrincipal);

                            if (!string.IsNullOrWhiteSpace(empresa.Eslogan))
                            {
                                datos.Item().Text(empresa.Eslogan).FontSize(8).Italic()
                                    .FontColor(Colors.Grey.Darken1);
                            }

                            foreach (var linea in recibo.Encabezado)
                            {
                                datos.Item().Text(linea).FontSize(8);
                            }
                        });

                        fila.ConstantItem(190).Border(1).BorderColor(ColorPrincipal).Padding(8).Column(caja =>
                        {
                            caja.Item().AlignCenter().Text(recibo.Titulo)
                                .FontSize(11).Bold().FontColor(ColorPrincipal);
                            caja.Item().AlignCenter().PaddingTop(3).Text(venta.NumeroFactura)
                                .FontSize(14).Bold();
                            caja.Item().AlignCenter().PaddingTop(3)
                                .Text(Formatos.FechaHora(venta.Fecha)).FontSize(8);

                            if (venta.Estado == EstadoVenta.Anulada)
                            {
                                caja.Item().AlignCenter().PaddingTop(3).Text("ANULADA")
                                    .Bold().FontColor(Colors.Red.Darken2);
                            }

                            if (recibo.EsCopia)
                            {
                                caja.Item().AlignCenter().PaddingTop(3).Text("COPIA").Bold();
                            }
                        });
                    });

                    if (recibo.Resolucion.Count > 0)
                    {
                        encabezado.Item().PaddingTop(4).Text(string.Join("  ·  ", recibo.Resolucion))
                            .FontSize(7).FontColor(Colors.Grey.Darken1);
                    }

                    encabezado.Item().PaddingTop(8).Background(Colors.Grey.Lighten4).Padding(6).Row(fila =>
                    {
                        fila.RelativeItem().Column(cliente =>
                        {
                            cliente.Item().Text("CLIENTE").FontSize(7).Bold().FontColor(Colors.Grey.Darken2);
                            cliente.Item().Text(venta.ClienteNombre).Bold();

                            if (!string.IsNullOrWhiteSpace(venta.ClienteDocumento))
                            {
                                cliente.Item().Text($"Documento: {venta.ClienteDocumento}").FontSize(8);
                            }

                            if (!string.IsNullOrWhiteSpace(venta.ClienteDireccion))
                            {
                                cliente.Item().Text(venta.ClienteDireccion).FontSize(8);
                            }

                            if (!string.IsNullOrWhiteSpace(venta.ClienteTelefono))
                            {
                                cliente.Item().Text($"Tel.: {venta.ClienteTelefono}").FontSize(8);
                            }
                        });

                        fila.ConstantItem(180).Column(pago =>
                        {
                            pago.Item().Text("CONDICIONES").FontSize(7).Bold().FontColor(Colors.Grey.Darken2);
                            pago.Item().Text($"Forma de pago: {venta.MetodoPago.Descripcion()}").FontSize(8);
                            pago.Item().Text($"Atendido por: {venta.UsuarioNombre}").FontSize(8);
                        });
                    });

                    encabezado.Item().PaddingTop(8);
                });

                pagina.Content().Column(contenido =>
                {
                    contenido.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(definicion =>
                        {
                            definicion.ConstantColumn(60);  // código
                            definicion.RelativeColumn(4);   // descripción
                            definicion.ConstantColumn(45);  // cantidad
                            definicion.ConstantColumn(70);  // valor unitario
                            definicion.ConstantColumn(50);  // descuento
                            definicion.ConstantColumn(45);  // IVA
                            definicion.ConstantColumn(75);  // total
                        });

                        tabla.Header(encabezado =>
                        {
                            foreach (var (titulo, derecha) in new[]
                                     {
                                         ("Código", false), ("Descripción", false), ("Cant.", true),
                                         ("V. unitario", true), ("Desc.", true), ("IVA", true), ("Total", true)
                                     })
                            {
                                var celda = encabezado.Cell().Background(ColorPrincipal).Padding(4);
                                var texto = celda.Text(titulo).FontColor(Colors.White).Bold().FontSize(8);

                                if (derecha)
                                {
                                    texto.AlignRight();
                                }
                            }
                        });

                        var indice = 0;

                        foreach (var linea in venta.Lineas)
                        {
                            Color fondo = indice % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                            CeldaFactura(tabla, fondo).Text(linea.Codigo).FontSize(8);
                            CeldaFactura(tabla, fondo).Text(linea.Descripcion).FontSize(8);
                            CeldaFactura(tabla, fondo).AlignRight()
                                .Text($"{Formatos.Cantidad(linea.Cantidad)} {linea.UnidadAbreviatura}").FontSize(8);
                            CeldaFactura(tabla, fondo).AlignRight()
                                .Text(Formatos.Moneda(linea.ValorUnitario)).FontSize(8);
                            CeldaFactura(tabla, fondo).AlignRight()
                                .Text(linea.ValorDescuento > 0 ? Formatos.Moneda(linea.ValorDescuento) : "—")
                                .FontSize(8);
                            CeldaFactura(tabla, fondo).AlignRight()
                                .Text(Formatos.Porcentaje(linea.PorcentajeIva, 0)).FontSize(8);
                            CeldaFactura(tabla, fondo).AlignRight()
                                .Text(Formatos.Moneda(linea.Total)).FontSize(8).Bold();

                            indice++;
                        }
                    });

                    contenido.Item().PaddingTop(10).Row(fila =>
                    {
                        fila.RelativeItem().Column(notas =>
                        {
                            if (!string.IsNullOrWhiteSpace(venta.Observaciones))
                            {
                                notas.Item().Text("Observaciones").FontSize(8).Bold();
                                notas.Item().Text(venta.Observaciones).FontSize(8);
                            }

                            if (venta.Estado == EstadoVenta.Anulada &&
                                !string.IsNullOrWhiteSpace(venta.MotivoAnulacion))
                            {
                                notas.Item().PaddingTop(4).Text("Motivo de anulación").FontSize(8).Bold();
                                notas.Item().Text(venta.MotivoAnulacion).FontSize(8)
                                    .FontColor(Colors.Red.Darken2);
                            }
                        });

                        fila.ConstantItem(230).Column(totales =>
                        {
                            FilaTotalCarta(totales, "Subtotal", venta.Subtotal);

                            if (venta.TotalDescuento > 0)
                            {
                                FilaTotalCarta(totales, "Descuentos", -venta.TotalDescuento);
                            }

                            FilaTotalCarta(totales, "IVA", venta.TotalIva);

                            totales.Item().PaddingTop(4).BorderTop(1).BorderColor(ColorPrincipal)
                                .PaddingTop(4).Row(linea =>
                                {
                                    linea.RelativeItem().Text("TOTAL A PAGAR").Bold().FontSize(11);
                                    linea.RelativeItem().AlignRight()
                                        .Text(Formatos.Moneda(venta.Total)).Bold().FontSize(13)
                                        .FontColor(ColorPrincipal);
                                });

                            if (venta.MetodoPago is MetodoPago.Efectivo or MetodoPago.Mixto)
                            {
                                FilaTotalCarta(totales, "Recibido", venta.MontoRecibido);
                                FilaTotalCarta(totales, "Cambio", venta.Cambio);
                            }
                        });
                    });

                    contenido.Item().PaddingTop(8).Text(recibo.TotalEnLetras).FontSize(8).Bold();

                    if (recibo.DiscriminaIva)
                    {
                        contenido.Item().PaddingTop(8).Width(300).Table(tabla =>
                        {
                            tabla.ColumnsDefinition(definicion =>
                            {
                                definicion.RelativeColumn(2);
                                definicion.RelativeColumn(2);
                                definicion.RelativeColumn(2);
                            });

                            foreach (var titulo in new[] { "Tarifa", "Base gravable", "IVA" })
                            {
                                tabla.Cell().Background(Colors.Grey.Lighten3).Padding(3)
                                    .Text(titulo).FontSize(7.5f).Bold();
                            }

                            foreach (var impuesto in recibo.Impuestos)
                            {
                                CeldaFactura(tabla, Colors.White).Text(impuesto.Tarifa).FontSize(8);
                                CeldaFactura(tabla, Colors.White).AlignRight().Text(impuesto.Base).FontSize(8);
                                CeldaFactura(tabla, Colors.White).AlignRight().Text(impuesto.Iva).FontSize(8);
                            }
                        });
                    }
                });

                pagina.Footer().PaddingTop(6).BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1)
                    .PaddingTop(4).Row(fila =>
                    {
                        fila.RelativeItem().Column(pie =>
                        {
                            pie.Item().Text(empresa.PieFactura).FontSize(8).FontColor(Colors.Grey.Darken1);
                            pie.Item().Text(recibo.EsCopia ? $"{recibo.LineaCopia}  ·  {recibo.LineaSoftware}" : recibo.LineaSoftware)
                                .FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                        });

                        fila.RelativeItem().AlignRight().Text(texto =>
                        {
                            texto.DefaultTextStyle(estilo => estilo.FontSize(7).FontColor(Colors.Grey.Darken1));
                            texto.Span("Página ");
                            texto.CurrentPageNumber();
                            texto.Span(" de ");
                            texto.TotalPages();
                        });
                    });
            });
        });

    private static IContainer CeldaFactura(TableDescriptor tabla, Color fondo) =>
        tabla.Cell().Background(fondo).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(3).PaddingHorizontal(4);

    private static void FilaTotalCarta(ColumnDescriptor columna, string etiqueta, decimal valor) =>
        columna.Item().Row(fila =>
        {
            fila.RelativeItem().Text(etiqueta).FontSize(9);
            fila.RelativeItem().AlignRight().Text(Formatos.Moneda(valor)).FontSize(9);
        });

    // ── Comprobante de compra ───────────────────────────────────────────────

    /// <summary>
    /// Cotización en carta. No es una factura y el documento lo dice de frente: lleva
    /// la fecha hasta la que valen esos precios, que es lo que evita la discusión de
    /// «pero usted me dijo» tres meses después.
    /// </summary>
    public async Task<string> GenerarCotizacionAsync(
        CotizacionDetalladaDto cotizacion, string? rutaDestino = null, CancellationToken ct = default)
    {
        var ruta = rutaDestino ?? RutasAplicacion.RutaTemporal(".pdf");
        var empresa = _configuracion.ObtenerEmpresa();

        await Task.Run(() =>
        {
            Document.Create(documento =>
            {
                documento.Page(pagina =>
                {
                    pagina.Size(PageSizes.Letter);
                    pagina.Margin(28);
                    pagina.DefaultTextStyle(estilo => estilo.FontSize(9).FontFamily(Fonts.Calibri));

                    pagina.Header().Column(encabezado =>
                    {
                        encabezado.Item().Row(fila =>
                        {
                            fila.RelativeItem().Column(datos =>
                            {
                                datos.Item().Text(empresa.Nombre).FontSize(16).Bold().FontColor(ColorPrincipal);

                                if (!string.IsNullOrWhiteSpace(empresa.Eslogan))
                                {
                                    datos.Item().Text(empresa.Eslogan).FontSize(8).Italic()
                                        .FontColor(Colors.Grey.Darken1);
                                }

                                foreach (var linea in new[]
                                         {
                                             empresa.LineaIdentificacion, empresa.LineaUbicacion,
                                             empresa.LineaContacto
                                         }.Where(l => !string.IsNullOrWhiteSpace(l)))
                                {
                                    datos.Item().Text(linea).FontSize(8);
                                }
                            });

                            fila.ConstantItem(190).Border(1).BorderColor(ColorPrincipal).Padding(8).Column(caja =>
                            {
                                caja.Item().AlignCenter().Text("COTIZACIÓN")
                                    .FontSize(11).Bold().FontColor(ColorPrincipal);
                                caja.Item().AlignCenter().PaddingTop(3).Text(cotizacion.Numero)
                                    .FontSize(14).Bold();
                                caja.Item().AlignCenter().PaddingTop(3)
                                    .Text(Formatos.Fecha(cotizacion.Fecha)).FontSize(8);
                                caja.Item().AlignCenter().PaddingTop(3)
                                    .Text($"Válida hasta el {Formatos.Fecha(cotizacion.FechaVence)}")
                                    .FontSize(8).Bold();
                            });
                        });

                        encabezado.Item().PaddingTop(8).Background(Colors.Grey.Lighten4).Padding(6).Column(cliente =>
                        {
                            cliente.Item().Text("CLIENTE").FontSize(7).Bold().FontColor(Colors.Grey.Darken2);
                            cliente.Item().Text(cotizacion.ClienteNombre).Bold();

                            if (!string.IsNullOrWhiteSpace(cotizacion.ClienteDocumento))
                            {
                                cliente.Item().Text($"Documento: {cotizacion.ClienteDocumento}").FontSize(8);
                            }

                            if (!string.IsNullOrWhiteSpace(cotizacion.ClienteTelefono))
                            {
                                cliente.Item().Text($"Tel.: {cotizacion.ClienteTelefono}").FontSize(8);
                            }
                        });

                        encabezado.Item().PaddingBottom(10);
                    });

                    pagina.Content().Column(contenido =>
                    {
                        contenido.Item().Table(tabla =>
                        {
                            tabla.ColumnsDefinition(definicion =>
                            {
                                definicion.ConstantColumn(60);
                                definicion.RelativeColumn(4);
                                definicion.ConstantColumn(45);
                                definicion.ConstantColumn(75);
                                definicion.ConstantColumn(50);
                                definicion.ConstantColumn(80);
                            });

                            tabla.Header(cabecera =>
                            {
                                foreach (var (titulo, derecha) in new[]
                                         {
                                             ("Código", false), ("Descripción", false), ("Cant.", true),
                                             ("V. unitario", true), ("Desc.", true), ("Total", true)
                                         })
                                {
                                    var celda = cabecera.Cell().Background(ColorPrincipal).Padding(4);
                                    var texto = celda.Text(titulo).FontColor(Colors.White).Bold().FontSize(8);

                                    if (derecha)
                                    {
                                        texto.AlignRight();
                                    }
                                }
                            });

                            var indice = 0;

                            foreach (var linea in cotizacion.Lineas)
                            {
                                Color fondo = indice % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                                CeldaFactura(tabla, fondo).Text(linea.Codigo).FontSize(8);
                                CeldaFactura(tabla, fondo).Text(linea.Descripcion).FontSize(8);
                                CeldaFactura(tabla, fondo).AlignRight()
                                    .Text(Formatos.Cantidad(linea.Cantidad)).FontSize(8);
                                CeldaFactura(tabla, fondo).AlignRight()
                                    .Text(Formatos.Moneda(linea.PrecioUnitario)).FontSize(8);
                                CeldaFactura(tabla, fondo).AlignRight()
                                    .Text(linea.PorcentajeDescuento > 0
                                        ? Formatos.Porcentaje(linea.PorcentajeDescuento, 0)
                                        : "—").FontSize(8);
                                CeldaFactura(tabla, fondo).AlignRight()
                                    .Text(Formatos.Moneda(linea.Total)).FontSize(8).Bold();

                                indice++;
                            }
                        });

                        contenido.Item().PaddingTop(10).Row(fila =>
                        {
                            fila.RelativeItem().Column(notas =>
                            {
                                if (!string.IsNullOrWhiteSpace(cotizacion.Observaciones))
                                {
                                    notas.Item().Text("Observaciones").FontSize(7).Bold()
                                        .FontColor(Colors.Grey.Darken2);
                                    notas.Item().Text(cotizacion.Observaciones).FontSize(8);
                                }

                                notas.Item().PaddingTop(8)
                                    .Text($"Estos precios se respetan hasta el {Formatos.Fecha(cotizacion.FechaVence)}.")
                                    .FontSize(8).Italic();

                                notas.Item().Text("Este documento no es una factura de venta.")
                                    .FontSize(7).FontColor(Colors.Grey.Darken1);
                            });

                            fila.ConstantItem(220).Column(totales =>
                            {
                                FilaTotalCarta(totales, "Subtotal", cotizacion.Subtotal);

                                if (cotizacion.TotalDescuento > 0)
                                {
                                    FilaTotalCarta(totales, "Descuentos", -cotizacion.TotalDescuento);
                                }

                                FilaTotalCarta(totales, "IVA", cotizacion.TotalIva);

                                totales.Item().PaddingTop(4).BorderTop(1).BorderColor(ColorPrincipal)
                                    .PaddingTop(4).Row(total =>
                                    {
                                        total.RelativeItem().Text("TOTAL").Bold().FontSize(12);
                                        total.RelativeItem().AlignRight()
                                            .Text(Formatos.Moneda(cotizacion.Total)).Bold().FontSize(12)
                                            .FontColor(ColorPrincipal);
                                    });
                            });
                        });
                    });

                    pagina.Footer().AlignCenter()
                        .Text($"Cotización elaborada por {cotizacion.UsuarioNombre}")
                        .FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            }).GeneratePdf(ruta);
        }, ct).ConfigureAwait(false);

        return ruta;
    }

    public async Task<string> GenerarComprobanteCompraAsync(
        CompraDetalladaDto compra, string? rutaDestino = null, CancellationToken ct = default)
    {
        var ruta = rutaDestino ?? RutasAplicacion.RutaTemporal(".pdf");
        var empresa = _configuracion.ObtenerEmpresa();

        await Task.Run(() =>
        {
            Document.Create(documento =>
            {
                documento.Page(pagina =>
                {
                    pagina.Size(PageSizes.Letter);
                    pagina.Margin(28);
                    pagina.DefaultTextStyle(estilo => estilo.FontSize(9).FontFamily(Fonts.Calibri));

                    pagina.Header().Column(encabezado =>
                    {
                        encabezado.Item().Row(fila =>
                        {
                            fila.RelativeItem().Column(datos =>
                            {
                                datos.Item().Text(empresa.Nombre).FontSize(15).Bold().FontColor(ColorPrincipal);
                                datos.Item().Text("Comprobante de compra").FontSize(11).SemiBold();
                            });

                            fila.ConstantItem(180).Column(caja =>
                            {
                                caja.Item().AlignRight().Text(compra.Numero).FontSize(14).Bold();
                                caja.Item().AlignRight().Text(Formatos.FechaHora(compra.Fecha)).FontSize(8);

                                if (compra.Estado == EstadoCompra.Anulada)
                                {
                                    caja.Item().AlignRight().Text("ANULADA").Bold()
                                        .FontColor(Colors.Red.Darken2);
                                }
                            });
                        });

                        encabezado.Item().PaddingTop(8).Background(Colors.Grey.Lighten4).Padding(6)
                            .Row(fila =>
                            {
                                fila.RelativeItem().Column(proveedor =>
                                {
                                    proveedor.Item().Text("PROVEEDOR").FontSize(7).Bold()
                                        .FontColor(Colors.Grey.Darken2);
                                    proveedor.Item().Text(compra.ProveedorNombre).Bold();

                                    if (!string.IsNullOrWhiteSpace(compra.ProveedorNit))
                                    {
                                        proveedor.Item().Text($"NIT: {compra.ProveedorNit}").FontSize(8);
                                    }

                                    if (!string.IsNullOrWhiteSpace(compra.ProveedorTelefono))
                                    {
                                        proveedor.Item().Text($"Tel.: {compra.ProveedorTelefono}").FontSize(8);
                                    }
                                });

                                fila.ConstantItem(200).Column(datos =>
                                {
                                    datos.Item().Text("DOCUMENTO").FontSize(7).Bold()
                                        .FontColor(Colors.Grey.Darken2);

                                    if (!string.IsNullOrWhiteSpace(compra.NumeroFacturaProveedor))
                                    {
                                        datos.Item()
                                            .Text($"Factura proveedor: {compra.NumeroFacturaProveedor}")
                                            .FontSize(8);
                                    }

                                    datos.Item().Text($"Registrada por: {compra.UsuarioNombre}").FontSize(8);
                                });
                            });

                        encabezado.Item().PaddingTop(8);
                    });

                    pagina.Content().Column(contenido =>
                    {
                        contenido.Item().Table(tabla =>
                        {
                            tabla.ColumnsDefinition(definicion =>
                            {
                                definicion.ConstantColumn(60);
                                definicion.RelativeColumn(4);
                                definicion.ConstantColumn(50);
                                definicion.ConstantColumn(75);
                                definicion.ConstantColumn(55);
                                definicion.ConstantColumn(55);
                                definicion.ConstantColumn(80);
                            });

                            tabla.Header(encabezado =>
                            {
                                foreach (var (titulo, derecha) in new[]
                                         {
                                             ("Código", false), ("Producto", false), ("Cant.", true),
                                             ("Costo", true), ("Desc.", true), ("IVA", true), ("Total", true)
                                         })
                                {
                                    var celda = encabezado.Cell().Background(ColorPrincipal).Padding(4);
                                    var texto = celda.Text(titulo).FontColor(Colors.White).Bold().FontSize(8);

                                    if (derecha)
                                    {
                                        texto.AlignRight();
                                    }
                                }
                            });

                            var indice = 0;

                            foreach (var linea in compra.Lineas)
                            {
                                Color fondo = indice % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                                CeldaFactura(tabla, fondo).Text(linea.Codigo).FontSize(8);
                                CeldaFactura(tabla, fondo).Text(linea.Descripcion).FontSize(8);
                                CeldaFactura(tabla, fondo).AlignRight()
                                    .Text($"{Formatos.Cantidad(linea.Cantidad)} {linea.UnidadAbreviatura}")
                                    .FontSize(8);
                                CeldaFactura(tabla, fondo).AlignRight()
                                    .Text(Formatos.Moneda(linea.ValorUnitario)).FontSize(8);
                                CeldaFactura(tabla, fondo).AlignRight()
                                    .Text(linea.ValorDescuento > 0
                                        ? Formatos.Moneda(linea.ValorDescuento)
                                        : "—").FontSize(8);
                                CeldaFactura(tabla, fondo).AlignRight()
                                    .Text(Formatos.Moneda(linea.ValorIva)).FontSize(8);
                                CeldaFactura(tabla, fondo).AlignRight()
                                    .Text(Formatos.Moneda(linea.Total)).FontSize(8).Bold();

                                indice++;
                            }
                        });

                        contenido.Item().PaddingTop(10).AlignRight().Width(240).Column(totales =>
                        {
                            FilaTotalCarta(totales, "Subtotal", compra.Subtotal);

                            if (compra.TotalDescuento > 0)
                            {
                                FilaTotalCarta(totales, "Descuentos", -compra.TotalDescuento);
                            }

                            FilaTotalCarta(totales, "IVA", compra.TotalIva);

                            totales.Item().PaddingTop(4).BorderTop(1).BorderColor(ColorPrincipal)
                                .PaddingTop(4).Row(linea =>
                                {
                                    linea.RelativeItem().Text("TOTAL").Bold().FontSize(11);
                                    linea.RelativeItem().AlignRight().Text(Formatos.Moneda(compra.Total))
                                        .Bold().FontSize(13).FontColor(ColorPrincipal);
                                });
                        });

                        if (!string.IsNullOrWhiteSpace(compra.Observaciones))
                        {
                            contenido.Item().PaddingTop(10).Text("Observaciones").FontSize(8).Bold();
                            contenido.Item().Text(compra.Observaciones).FontSize(8);
                        }
                    });

                    pagina.Footer().AlignCenter().Text(texto =>
                    {
                        texto.DefaultTextStyle(estilo => estilo.FontSize(7).FontColor(Colors.Grey.Darken1));
                        texto.Span("Página ");
                        texto.CurrentPageNumber();
                        texto.Span(" de ");
                        texto.TotalPages();
                    });
                });
            }).GeneratePdf(ruta);
        }, ct).ConfigureAwait(false);

        return ruta;
    }

    // ── Etiquetas de producto ───────────────────────────────────────────────

    public async Task<string> GenerarEtiquetasAsync(
        IEnumerable<EtiquetaProducto> etiquetas, string? rutaDestino = null, CancellationToken ct = default)
    {
        var ruta = rutaDestino ?? RutasAplicacion.RutaTemporal(".pdf");
        var empresa = _configuracion.ObtenerEmpresa();

        // Se expande la lista según las copias pedidas de cada etiqueta.
        var expandidas = etiquetas
            .SelectMany(e => Enumerable.Repeat(e, Math.Max(e.Copias, 1)))
            .ToList();

        if (expandidas.Count == 0)
        {
            throw new Domain.Exceptions.NegocioException("No hay etiquetas para imprimir.");
        }

        // Las imágenes se generan una sola vez por contenido y se reutilizan en cada copia.
        var imagenes = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var etiqueta in expandidas)
        {
            var contenido = string.IsNullOrWhiteSpace(etiqueta.CodigoBarras)
                ? etiqueta.Codigo
                : etiqueta.CodigoBarras;

            if (!imagenes.ContainsKey(contenido))
            {
                imagenes[contenido] = _codigoBarras.GenerarPng(contenido,
                    SimbologiaCodigoBarras.Automatica, 420, 110);
            }
        }

        await Task.Run(() =>
        {
            Document.Create(documento =>
            {
                documento.Page(pagina =>
                {
                    pagina.Size(PageSizes.Letter);
                    pagina.Margin(12, Unit.Millimetre);
                    pagina.DefaultTextStyle(estilo => estilo.FontSize(7).FontFamily(Fonts.Calibri));

                    pagina.Content().Column(columna =>
                    {
                        columna.Spacing(4, Unit.Millimetre);

                        // Tres etiquetas por fila, tamaño aproximado 60 × 35 mm.
                        foreach (var grupo in expandidas.Chunk(3))
                        {
                            columna.Item().Row(fila =>
                            {
                                fila.Spacing(4, Unit.Millimetre);

                                foreach (var etiqueta in grupo)
                                {
                                    var contenido = string.IsNullOrWhiteSpace(etiqueta.CodigoBarras)
                                        ? etiqueta.Codigo
                                        : etiqueta.CodigoBarras;

                                    fila.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Medium)
                                        .Padding(4).Column(celda =>
                                        {
                                            celda.Item().AlignCenter().Text(empresa.Nombre)
                                                .FontSize(6).FontColor(Colors.Grey.Darken1);

                                            celda.Item().AlignCenter().PaddingTop(1)
                                                .Text(etiqueta.Nombre).FontSize(7.5f).Bold();

                                            celda.Item().PaddingTop(2).AlignCenter()
                                                .Height(11, Unit.Millimetre).Image(imagenes[contenido]);

                                            celda.Item().AlignCenter().Text(contenido).FontSize(6);

                                            celda.Item().PaddingTop(2).AlignCenter()
                                                .Text(Formatos.Moneda(etiqueta.Precio))
                                                .FontSize(12).Bold().FontColor(ColorPrincipal);

                                            if (!string.IsNullOrWhiteSpace(etiqueta.UnidadAbreviatura))
                                            {
                                                celda.Item().AlignCenter()
                                                    .Text($"por {etiqueta.UnidadAbreviatura}")
                                                    .FontSize(6).FontColor(Colors.Grey.Darken1);
                                            }
                                        });
                                }

                                // Rellena la fila incompleta para conservar el ancho de las celdas.
                                for (var i = grupo.Length; i < 3; i++)
                                {
                                    fila.RelativeItem();
                                }
                            });
                        }
                    });
                });
            }).GeneratePdf(ruta);
        }, ct).ConfigureAwait(false);

        return ruta;
    }

    // ── Arqueo de caja ──────────────────────────────────────────────────────

    public async Task<string> GenerarArqueoCajaAsync(
        CajaSesionDto sesion,
        ArqueoCajaDto arqueo,
        IReadOnlyList<MovimientoCajaDto> movimientos,
        string? rutaDestino = null,
        CancellationToken ct = default)
    {
        var ruta = rutaDestino ?? RutasAplicacion.RutaTemporal(".pdf");
        var empresa = _configuracion.ObtenerEmpresa();

        await Task.Run(() =>
        {
            Document.Create(documento =>
            {
                documento.Page(pagina =>
                {
                    pagina.Size(PageSizes.Letter);
                    pagina.Margin(28);
                    pagina.DefaultTextStyle(estilo => estilo.FontSize(9).FontFamily(Fonts.Calibri));

                    pagina.Header().Column(encabezado =>
                    {
                        encabezado.Item().Text(empresa.Nombre).FontSize(15).Bold().FontColor(ColorPrincipal);
                        encabezado.Item().Text($"Arqueo de caja — sesión N.º {sesion.Id}")
                            .FontSize(11).SemiBold();
                        encabezado.Item().Text(
                                $"Apertura: {Formatos.FechaHora(sesion.FechaApertura)} · {sesion.UsuarioApertura}")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);

                        if (sesion.FechaCierre is { } cierre)
                        {
                            encabezado.Item().Text(
                                    $"Cierre: {Formatos.FechaHora(cierre)} · {sesion.UsuarioCierre}")
                                .FontSize(8).FontColor(Colors.Grey.Darken1);
                        }

                        encabezado.Item().PaddingTop(6).LineHorizontal(1).LineColor(ColorPrincipal);
                        encabezado.Item().PaddingTop(8);
                    });

                    pagina.Content().Column(contenido =>
                    {
                        contenido.Item().Column(resumen =>
                        {
                            FilaTotalCarta(resumen, "Base inicial", arqueo.MontoInicial);
                            FilaTotalCarta(resumen, "Ventas en efectivo", arqueo.VentasEfectivo);
                            FilaTotalCarta(resumen, "Ventas con tarjeta", arqueo.VentasTarjeta);
                            FilaTotalCarta(resumen, "Ventas por transferencia", arqueo.VentasTransferencia);

                            // Las billeteras solo se listan si hubo: en un negocio que no
                            // las usa, dos renglones en cero solo estorban el arqueo.
                            if (arqueo.VentasNequi > 0)
                            {
                                FilaTotalCarta(resumen, "Ventas por Nequi", arqueo.VentasNequi);
                            }

                            if (arqueo.VentasDaviplata > 0)
                            {
                                FilaTotalCarta(resumen, "Ventas por Daviplata", arqueo.VentasDaviplata);
                            }

                            FilaTotalCarta(resumen, "Ventas a crédito", arqueo.VentasCredito);
                            FilaTotalCarta(resumen, "Otros ingresos", arqueo.Ingresos);
                            FilaTotalCarta(resumen, "Egresos", -arqueo.Egresos);

                            resumen.Item().PaddingTop(4).BorderTop(1).BorderColor(Colors.Grey.Darken1)
                                .PaddingTop(4).Row(fila =>
                                {
                                    fila.RelativeItem().Text("Efectivo esperado").Bold();
                                    fila.RelativeItem().AlignRight()
                                        .Text(Formatos.Moneda(arqueo.MontoEsperado)).Bold();
                                });

                            if (sesion.FechaCierre is not null)
                            {
                                FilaTotalCarta(resumen, "Efectivo contado", sesion.MontoReal);

                                resumen.Item().PaddingTop(4).Row(fila =>
                                {
                                    fila.RelativeItem().Text($"Diferencia ({sesion.DiferenciaTexto})")
                                        .Bold().FontSize(11);
                                    fila.RelativeItem().AlignRight()
                                        .Text(Formatos.Moneda(sesion.Diferencia)).Bold().FontSize(12)
                                        .FontColor(sesion.Diferencia == 0
                                            ? Colors.Green.Darken2
                                            : Colors.Red.Darken2);
                                });
                            }
                        });

                        contenido.Item().PaddingTop(14).Text("Movimientos de la sesión")
                            .FontSize(10).Bold();

                        contenido.Item().PaddingTop(4).Table(tabla =>
                        {
                            tabla.ColumnsDefinition(definicion =>
                            {
                                definicion.ConstantColumn(85);
                                definicion.ConstantColumn(95);
                                definicion.RelativeColumn(3);
                                definicion.ConstantColumn(85);
                            });

                            tabla.Header(encabezado =>
                            {
                                foreach (var (titulo, derecha) in new[]
                                         {
                                             ("Hora", false), ("Tipo", false),
                                             ("Concepto", false), ("Monto", true)
                                         })
                                {
                                    var celda = encabezado.Cell().Background(ColorPrincipal).Padding(4);
                                    var texto = celda.Text(titulo).FontColor(Colors.White).Bold().FontSize(8);

                                    if (derecha)
                                    {
                                        texto.AlignRight();
                                    }
                                }
                            });

                            var indice = 0;

                            foreach (var movimiento in movimientos)
                            {
                                Color fondo = indice % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                                CeldaFactura(tabla, fondo).Text(Formatos.Hora(movimiento.Fecha)).FontSize(8);
                                CeldaFactura(tabla, fondo).Text(movimiento.TipoTexto).FontSize(8);
                                CeldaFactura(tabla, fondo).Text(movimiento.Concepto).FontSize(8);
                                CeldaFactura(tabla, fondo).AlignRight()
                                    .Text(Formatos.Moneda(movimiento.Monto)).FontSize(8);

                                indice++;
                            }
                        });

                        if (!string.IsNullOrWhiteSpace(sesion.ObservacionesCierre))
                        {
                            contenido.Item().PaddingTop(10).Text("Observaciones del cierre")
                                .FontSize(8).Bold();
                            contenido.Item().Text(sesion.ObservacionesCierre).FontSize(8);
                        }

                        contenido.Item().PaddingTop(30).Row(fila =>
                        {
                            fila.RelativeItem().Column(firma =>
                            {
                                firma.Item().LineHorizontal(0.5f);
                                firma.Item().AlignCenter().Text("Responsable de caja").FontSize(8);
                            });

                            fila.ConstantItem(40);

                            fila.RelativeItem().Column(firma =>
                            {
                                firma.Item().LineHorizontal(0.5f);
                                firma.Item().AlignCenter().Text("Revisado por").FontSize(8);
                            });
                        });
                    });
                });
            }).GeneratePdf(ruta);
        }, ct).ConfigureAwait(false);

        return ruta;
    }
}
