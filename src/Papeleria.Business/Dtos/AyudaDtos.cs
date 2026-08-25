namespace Papeleria.Business.Dtos;

/// <summary>
/// Paso de la puesta en marcha. Lleva a dónde hay que ir y por qué importa, para
/// que el dueño no tenga que adivinar en qué orden montar su papelería.
/// </summary>
public class PasoTutorialDto
{
    public required int Numero { get; init; }

    public required string Titulo { get; init; }

    /// <summary>Qué se hace en este paso, en una frase.</summary>
    public required string Descripcion { get; init; }

    /// <summary>Por qué conviene hacerlo; sin esto un paso parece burocracia.</summary>
    public required string PorQue { get; init; }

    public required string Icono { get; init; }

    /// <summary>Módulo al que lleva el botón «Ir».</summary>
    public required string Modulo { get; init; }

    /// <summary>El sistema ya encontró hecho este paso.</summary>
    public bool Completado { get; init; }

    /// <summary>Un paso opcional no impide empezar a vender.</summary>
    public bool EsOpcional { get; init; }

    /// <summary>Dato concreto de lo encontrado: «3 productos registrados».</summary>
    public string? Detalle { get; init; }
}

/// <summary>Estado de la puesta en marcha completa.</summary>
public class ProgresoTutorialDto
{
    public IReadOnlyList<PasoTutorialDto> Pasos { get; init; } = Array.Empty<PasoTutorialDto>();

    public int Completados => Pasos.Count(p => p.Completado);

    public int Total => Pasos.Count;

    /// <summary>Pasos imprescindibles que aún faltan.</summary>
    public int PendientesEsenciales => Pasos.Count(p => !p.Completado && !p.EsOpcional);

    public double Porcentaje => Total == 0 ? 0 : Math.Round((double)Completados / Total * 100, 0);

    /// <summary>Parte pendiente; junto con el porcentaje reparte el ancho de la barra.</summary>
    public double PorcentajeRestante => 100 - Porcentaje;

    public bool TodoListo => PendientesEsenciales == 0;

    /// <summary>Siguiente paso por resolver, que es el que se destaca.</summary>
    public PasoTutorialDto? Siguiente =>
        Pasos.FirstOrDefault(p => !p.Completado && !p.EsOpcional) ??
        Pasos.FirstOrDefault(p => !p.Completado);
}
