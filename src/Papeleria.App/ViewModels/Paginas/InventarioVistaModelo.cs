using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Papeleria.App.Infrastructure;
using Papeleria.App.ViewModels.Dialogos;
using Papeleria.Business.Dtos;
using Papeleria.Business.Security;
using Papeleria.Business.Services;
using Papeleria.Business.Services.Catalogos;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Entities;

namespace Papeleria.App.ViewModels.Paginas;

/// <summary>
/// Consulta de existencias y movimientos manuales de inventario:
/// entradas, salidas, ajustes y traslados de ubicación.
/// </summary>
public partial class InventarioVistaModelo : PaginaVistaModelo
{
    private readonly IServicioInventario _inventario;
    private readonly IServicioCategorias _categorias;
    private readonly IServicioDialogos _dialogos;
    private readonly IContextoSesion _sesion;

    public InventarioVistaModelo(
        IServicioInventario inventario,
        IServicioCategorias categorias,
        IServicioDialogos dialogos,
        IContextoSesion sesion)
    {
        _inventario = inventario;
        _categorias = categorias;
        _dialogos = dialogos;
        _sesion = sesion;

        Titulo = "Inventario";
        Subtitulo = "Existencias actuales y movimientos manuales";

        Paginador.PaginaCambiada += (_, _) => _ = BuscarAsync();

        WeakReferenceMessenger.Default.Register<InventarioVistaModelo, InventarioCambiadoMensaje>(
            this, (destinatario, mensaje) => { _ = destinatario.BuscarAsync(); });
    }

    public override string Modulo => Modulos.Inventario;

    public Paginador Paginador { get; } = new();

    public ObservableCollection<ProductoListadoDto> Existencias { get; } = new();

    public ObservableCollection<Categoria> Categorias { get; } = new();

    public IReadOnlyList<KeyValuePair<EstadoStock?, string>> EstadosStock { get; } =
        new List<KeyValuePair<EstadoStock?, string>>
        {
            new(null, "Todas las existencias"),
            new(EstadoStock.Agotado, "Agotados"),
            new(EstadoStock.Bajo, "Bajo el mínimo"),
            new(EstadoStock.Normal, "Existencias normales"),
            new(EstadoStock.Exceso, "Sobre el máximo")
        };

    [ObservableProperty] private string? _textoBusqueda;
    [ObservableProperty] private int? _categoriaSeleccionadaId;
    [ObservableProperty] private EstadoStock? _estadoSeleccionado;
    [ObservableProperty] private ProductoListadoDto? _productoSeleccionado;
    [ObservableProperty] private ResumenInventarioDto? _resumen;

    public bool PuedeMover => _sesion.Puede(Modulos.Inventario, AccionPermiso.Editar);

    public bool HaySeleccion => ProductoSeleccionado is not null;

    partial void OnTextoBusquedaChanged(string? value) => ReiniciarBusqueda();
    partial void OnCategoriaSeleccionadaIdChanged(int? value) => ReiniciarBusqueda();
    partial void OnEstadoSeleccionadoChanged(EstadoStock? value) => ReiniciarBusqueda();

    partial void OnProductoSeleccionadoChanged(ProductoListadoDto? value) =>
        OnPropertyChanged(nameof(HaySeleccion));

    private void ReiniciarBusqueda()
    {
        Paginador.Reiniciar();
        _ = BuscarAsync();
    }

    public override async Task CargarAsync()
    {
        await CargarCategoriasAsync().ConfigureAwait(true);
        await BuscarAsync().ConfigureAwait(true);
    }

    private Task CargarCategoriasAsync() => EjecutarAsync(async () =>
    {
        var categorias = await _categorias.ListarAsync().ConfigureAwait(true);

        Categorias.Clear();
        Categorias.Add(new Categoria { Id = 0, Nombre = "Todas las categorías" });

        foreach (var categoria in categorias)
        {
            Categorias.Add(categoria);
        }
    }, "No se pudieron cargar las categorías.");

    [RelayCommand]
    private Task BuscarAsync() => EjecutarAsync(async () =>
    {
        var resultado = await _inventario.ConsultarExistenciasAsync(new FiltroProductos
        {
            Texto = TextoBusqueda,
            CategoriaId = CategoriaSeleccionadaId,
            Estado = EstadoSeleccionado,
            SoloActivos = true,
            Pagina = Paginador.Pagina,
            TamanoPagina = Paginador.TamanoPagina,
            OrdenarPor = nameof(ProductoListadoDto.Nombre)
        }).ConfigureAwait(true);

        Existencias.Clear();

        foreach (var producto in resultado.Elementos)
        {
            Existencias.Add(producto);
        }

        Paginador.Actualizar(resultado);

        Resumen = await _inventario.ObtenerResumenAsync().ConfigureAwait(true);
    }, "No se pudieron consultar las existencias.");

    [RelayCommand]
    private Task RegistrarEntradaAsync() => AbrirMovimientoAsync(TipoMovimientoManual.Entrada);

    [RelayCommand]
    private Task RegistrarSalidaAsync() => AbrirMovimientoAsync(TipoMovimientoManual.Salida);

    [RelayCommand]
    private Task RegistrarAjusteAsync() => AbrirMovimientoAsync(TipoMovimientoManual.Ajuste);

    [RelayCommand]
    private Task RegistrarTransferenciaAsync() => AbrirMovimientoAsync(TipoMovimientoManual.Transferencia);

    private async Task AbrirMovimientoAsync(TipoMovimientoManual tipo)
    {
        if (ProductoSeleccionado is null)
        {
            await _dialogos.InformarAsync(
                "Seleccione un producto",
                "Elija primero el producto sobre el que desea registrar el movimiento.").ConfigureAwait(true);

            return;
        }

        if (!PuedeMover)
        {
            return;
        }

        var producto = ProductoSeleccionado;
        MovimientoInventarioDialogoVistaModelo? dialogo = null;

        dialogo = new MovimientoInventarioDialogoVistaModelo(
            _dialogos,
            async () =>
            {
                switch (tipo)
                {
                    case TipoMovimientoManual.Entrada:
                        await _inventario.RegistrarEntradaAsync(new SolicitudMovimientoInventario
                        {
                            ProductoId = producto.Id,
                            Cantidad = dialogo!.Cantidad,
                            Motivo = dialogo.Motivo,
                            DocumentoReferencia = dialogo.DocumentoReferencia,
                            CostoUnitario = dialogo.CostoUnitario
                        }).ConfigureAwait(true);
                        break;

                    case TipoMovimientoManual.Salida:
                        await _inventario.RegistrarSalidaAsync(new SolicitudMovimientoInventario
                        {
                            ProductoId = producto.Id,
                            Cantidad = dialogo!.Cantidad,
                            Motivo = dialogo.Motivo,
                            DocumentoReferencia = dialogo.DocumentoReferencia
                        }).ConfigureAwait(true);
                        break;

                    case TipoMovimientoManual.Ajuste:
                        await _inventario.RegistrarAjusteAsync(
                            producto.Id, dialogo!.StockReal, dialogo.Motivo).ConfigureAwait(true);
                        break;

                    default:
                        await _inventario.RegistrarTransferenciaAsync(new SolicitudTransferencia
                        {
                            ProductoId = producto.Id,
                            Cantidad = dialogo!.Cantidad,
                            UbicacionOrigen = dialogo.UbicacionOrigen,
                            UbicacionDestino = dialogo.UbicacionDestino,
                            Observaciones = dialogo.Motivo
                        }).ConfigureAwait(true);
                        break;
                }
            },
            tipo,
            producto.Nombre,
            producto.StockActual,
            producto.Costo,
            producto.Ubicacion);

        if (await _dialogos.MostrarAsync(dialogo).ConfigureAwait(true) is true)
        {
            _dialogos.Notificar("Movimiento registrado en el kardex.");
            WeakReferenceMessenger.Default.Send(new InventarioCambiadoMensaje(producto.Id));
            await BuscarAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private static void VerKardex() =>
        WeakReferenceMessenger.Default.Send(new NavegarMensaje(Modulos.Kardex));
}
