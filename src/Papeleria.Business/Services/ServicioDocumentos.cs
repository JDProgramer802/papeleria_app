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
        CancellationToken ct = default)
    {
        var ruta = rutaDestino ?? RutasAplicacion.RutaTemporal(".pdf");
        var empresa = _configuracion.ObtenerEmpresa();

        await Task.Run(() =>
        {
            var documento = formato == FormatoFactura.Recibo80mm
                ? ConstruirRecibo(venta, empresa)
                : ConstruirFacturaCarta(venta, empresa);

            documento.GeneratePdf(ruta);
        }, ct).ConfigureAwait(false);

        return ruta;
    }

    /// <summary>Tirilla continua de 80 mm: el alto crece con el número de líneas.</summary>
    private IDocument ConstruirRecibo(VentaDetalladaDto venta, DatosEmpresa empresa) =>
        Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.ContinuousSize(80, Unit.Millimetre);
                pagina.MarginHorizontal(4, Unit.Millimetre);
                pagina.MarginVertical(5, Unit.Millimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(7.5f).FontFamily(Fonts.Consolas));

                pagina.Content().Column(columna =>
                {
                    columna.Item().AlignCenter().Text(empresa.Nombre).Bold().FontSize(10);

                    if (!string.IsNullOrWhiteSpace(empresa.LineaIdentificacion))
                    {
                        columna.Item().AlignCenter().Text(empresa.LineaIdentificacion).FontSize(7);
                    }

                    if (!string.IsNullOrWhiteSpace(empresa.LineaUbicacion))
                    {
                        columna.Item().AlignCenter().Text(empresa.LineaUbicacion).FontSize(7);
                    }

                    if (!string.IsNullOrWhiteSpace(empresa.LineaContacto))
                    {
                        columna.Item().AlignCenter().Text(empresa.LineaContacto).FontSize(7);
                    }

                    if (!string.IsNullOrWhiteSpace(empresa.Resolucion))
                    {
                        columna.Item().AlignCenter().PaddingTop(2).Text(empresa.Resolucion).FontSize(6);
                    }

                    columna.Item().PaddingVertical(4).LineHorizontal(0.5f);

                    columna.Item().Text($"FACTURA {venta.NumeroFactura}").Bold().FontSize(9);
                    columna.Item().Text($"Fecha:   {Formatos.FechaHora(venta.Fecha)}");
                    columna.Item().Text($"Cajero:  {venta.UsuarioNombre}");
                    columna.Item().Text($"Cliente: {venta.ClienteNombre}");

                    if (!string.IsNullOrWhiteSpace(venta.ClienteDocumento))
                    {
                        columna.Item().Text($"Doc.:    {venta.ClienteDocumento}");
                    }

                    if (venta.Estado == EstadoVenta.Anulada)
                    {
                        columna.Item().PaddingTop(3).AlignCenter()
                            .Text("*** FACTURA ANULADA ***").Bold().FontColor(Colors.Red.Darken2);
                    }

                    columna.Item().PaddingVertical(4).LineHorizontal(0.5f);

                    foreach (var linea in venta.Lineas)
                    {
                        columna.Item().Text(linea.Descripcion).Bold();

                        columna.Item().Row(fila =>
                        {
                            fila.RelativeItem(3).Text(
                                $"{Formatos.Cantidad(linea.Cantidad)} x {Formatos.Moneda(linea.ValorUnitario)}");
                            fila.RelativeItem(2).AlignRight().Text(Formatos.Moneda(linea.Subtotal));
                        });

                        if (linea.ValorDescuento > 0)
                        {
                            columna.Item().Row(fila =>
                            {
                                fila.RelativeItem(3).Text($"  Descuento {Formatos.Porcentaje(linea.PorcentajeDescuento)}");
                                fila.RelativeItem(2).AlignRight().Text($"-{Formatos.Moneda(linea.ValorDescuento)}");
                            });
                        }
                    }

                    columna.Item().PaddingVertical(4).LineHorizontal(0.5f);

                    AgregarTotalRecibo(columna, "Subtotal", venta.Subtotal);

                    if (venta.TotalDescuento > 0)
                    {
                        AgregarTotalRecibo(columna, "Descuentos", -venta.TotalDescuento);
                    }

                    if (venta.TotalIva > 0)
                    {
                        AgregarTotalRecibo(columna, "IVA", venta.TotalIva);
                    }

                    columna.Item().PaddingTop(2).Row(fila =>
                    {
                        fila.RelativeItem().Text("TOTAL").Bold().FontSize(11);
                        fila.RelativeItem().AlignRight().Text(Formatos.Moneda(venta.Total)).Bold().FontSize(11);
                    });

                    columna.Item().PaddingTop(3).Text($"Forma de pago: {venta.MetodoPago.Descripcion()}");

                    if (venta.MetodoPago is MetodoPago.Efectivo or MetodoPago.Mixto)
                    {
                        AgregarTotalRecibo(columna, "Recibido", venta.MontoRecibido);
                        AgregarTotalRecibo(columna, "Cambio", venta.Cambio);
                    }

                    columna.Item().PaddingTop(6).AlignCenter()
                        .Text($"Artículos: {venta.CantidadArticulos}").FontSize(7);

                    if (!string.IsNullOrWhiteSpace(empresa.PieFactura))
                    {
                        columna.Item().PaddingTop(4).AlignCenter().Text(empresa.PieFactura).FontSize(8).Bold();
                    }

                    // El código del documento permite recuperar la venta escaneando la tirilla.
                    var imagen = _codigoBarras.GenerarPng(venta.NumeroFactura,
                        SimbologiaCodigoBarras.Code128, 420, 90);

                    columna.Item().PaddingTop(6).AlignCenter().Width(60, Unit.Millimetre).Image(imagen);

                    columna.Item().PaddingTop(2).AlignCenter().Text(venta.NumeroFactura).FontSize(7);
                });
            });
        });

    private static void AgregarTotalRecibo(ColumnDescriptor columna, string etiqueta, decimal valor) =>
        columna.Item().Row(fila =>
        {
            fila.RelativeItem().Text(etiqueta);
            fila.RelativeItem().AlignRight().Text(Formatos.Moneda(valor));
        });

    /// <summary>Formato carta, con encabezado de empresa y tabla de detalle.</summary>
    private IDocument ConstruirFacturaCarta(VentaDetalladaDto venta, DatosEmpresa empresa) =>
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
                                         empresa.LineaIdentificacion, empresa.LineaUbicacion, empresa.LineaContacto
                                     }.Where(l => !string.IsNullOrWhiteSpace(l)))
                            {
                                datos.Item().Text(linea).FontSize(8);
                            }
                        });

                        fila.ConstantItem(190).Border(1).BorderColor(ColorPrincipal).Padding(8).Column(caja =>
                        {
                            caja.Item().AlignCenter().Text("FACTURA DE VENTA")
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
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(empresa.Resolucion))
                    {
                        encabezado.Item().PaddingTop(4).Text(empresa.Resolucion)
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
                });

                pagina.Footer().PaddingTop(6).BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1)
                    .PaddingTop(4).Row(fila =>
                    {
                        fila.RelativeItem().Text(empresa.PieFactura)
                            .FontSize(8).FontColor(Colors.Grey.Darken1);

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
