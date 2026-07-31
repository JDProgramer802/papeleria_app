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
