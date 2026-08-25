namespace Papeleria.Business.Dtos;

/// <summary>Punto de una serie temporal de los gráficos del dashboard.</summary>
public record PuntoSerie(string Etiqueta, decimal Valor, DateTime Periodo);

/// <summary>Nivel de importancia de un aviso del dashboard.</summary>
public enum NivelAlerta
{
    Informacion = 0,
    Advertencia = 1,
    Critica = 2
}

/// <summary>Aviso accionable mostrado en el panel de alertas.</summary>
public class AlertaDto
{
    public required NivelAlerta Nivel { get; init; }

    public required string Titulo { get; init; }

    public required string Detalle { get; init; }

    /// <summary>Módulo al que navega la alerta al pulsarla.</summary>
    public string? ModuloDestino { get; init; }

    public string Icono => Nivel switch
    {
        NivelAlerta.Critica => "AlertCircle",
        NivelAlerta.Advertencia => "AlertOutline",
        _ => "InformationOutline"
    };
}

/// <summary>Conjunto completo de indicadores que alimenta el dashboard.</summary>
public class ResumenDashboardDto
{
    // ── Tarjetas principales ────────────────────────────────────────────────
    public int TotalProductos { get; init; }

    public decimal ValorInventario { get; init; }

    public decimal ComprasDelMes { get; init; }

    public decimal VentasDelMes { get; init; }

    public decimal GananciasDelMes { get; init; }

    public int ProductosBajoStock { get; init; }

    public int ProductosAgotados { get; init; }

    public int TotalProveedores { get; init; }

    public int TotalClientes { get; init; }

    // ── Comparativa contra el mes anterior ──────────────────────────────────
    public decimal VariacionVentas { get; init; }

    public decimal VariacionCompras { get; init; }

    public decimal VariacionGanancias { get; init; }

    // ── Actividad del día ───────────────────────────────────────────────────
    public int VentasDelDia { get; init; }

    public decimal MontoVentasDelDia { get; init; }

    public decimal TicketPromedio { get; init; }

    public bool CajaAbierta { get; init; }

    // ── Estado de la caja ───────────────────────────────────────────────────

    /// <summary>Hora en que se abrió el turno vigente.</summary>
    public DateTime? CajaAbiertaDesde { get; init; }

    public string? CajaAbiertaPor { get; init; }

    /// <summary>Efectivo que debería haber en el cajón ahora mismo.</summary>
    public decimal EfectivoEnCaja { get; init; }

    /// <summary>El usuario tiene permiso para ver las cifras de caja.</summary>
    public bool PuedeVerCaja { get; init; }

    public string CajaDesdeTexto => CajaAbiertaDesde is { } desde
        ? $"Abierta desde las {desde:HH:mm}" + (string.IsNullOrWhiteSpace(CajaAbiertaPor)
            ? string.Empty
            : $" por {CajaAbiertaPor}")
        : "Sin turno abierto";

    /// <summary>Horas que lleva abierto el turno; sirve para avisar de un olvido.</summary>
    public double HorasCajaAbierta =>
        CajaAbiertaDesde is { } desde ? (DateTime.Now - desde).TotalHours : 0;

    // ── Cartera ─────────────────────────────────────────────────────────────

    /// <summary>Total que los clientes deben por ventas a crédito.</summary>
    public decimal SaldoCartera { get; init; }

    /// <summary>Parte de esa deuda con más de sesenta días.</summary>
    public decimal CarteraVencida { get; init; }

    public int ClientesConDeuda { get; init; }

    public bool PuedeVerCartera { get; init; }

    public bool HayCarteraVencida => CarteraVencida > 0;

    // ── Comparación con el año anterior ─────────────────────────────────────

    /// <summary>Ventas del mismo mes del año pasado, la única comparación honesta.</summary>
    public decimal VentasMismoMesAnioAnterior { get; init; }

    public decimal VariacionInteranual { get; init; }

    /// <summary>Hay ventas de hace un año con las que comparar.</summary>
    public bool HayHistorialAnual { get; init; }

    /// <summary>Ventas del mismo día de la semana pasada, para dar contexto a las de hoy.</summary>
    public decimal MontoMismoDiaSemanaAnterior { get; init; }

    public decimal VariacionDiaria { get; init; }

    public bool HayReferenciaDiaria => MontoMismoDiaSemanaAnterior > 0;

    // ── Dinero quieto y precios en pérdida ──────────────────────────────────

    /// <summary>Artículos con existencias que no se venden desde hace noventa días.</summary>
    public int ProductosSinRotacion { get; init; }

    /// <summary>Lo que valen a costo esos artículos parados.</summary>
    public decimal ValorSinRotacion { get; init; }

    /// <summary>Artículos cuyo precio de venta quedó por debajo del costo.</summary>
    public int ProductosBajoCosto { get; init; }

    public bool HayProductosBajoCosto => ProductosBajoCosto > 0;

    // ── Series y listados ───────────────────────────────────────────────────
    public IReadOnlyList<PuntoSerie> SerieVentas { get; init; } = Array.Empty<PuntoSerie>();

    public IReadOnlyList<PuntoSerie> SerieCompras { get; init; } = Array.Empty<PuntoSerie>();

    public IReadOnlyList<ProductoVendidoDto> ProductosMasVendidos { get; init; } =
        Array.Empty<ProductoVendidoDto>();

    public IReadOnlyList<MovimientoKardexDto> MovimientosRecientes { get; init; } =
        Array.Empty<MovimientoKardexDto>();

    public IReadOnlyList<AlertaDto> Alertas { get; init; } = Array.Empty<AlertaDto>();

    public DateTime GeneradoEn { get; init; } = DateTime.Now;
}
