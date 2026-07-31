namespace Papeleria.Domain.Common;

/// <summary>Página de resultados junto con los metadatos necesarios para el paginador de la UI.</summary>
public class ResultadoPaginado<T>
{
    public ResultadoPaginado(IReadOnlyList<T> elementos, int totalRegistros, int pagina, int tamanoPagina)
    {
        Elementos = elementos;
        TotalRegistros = totalRegistros;
        Pagina = pagina < 1 ? 1 : pagina;
        TamanoPagina = tamanoPagina < 1 ? 1 : tamanoPagina;
    }

    public IReadOnlyList<T> Elementos { get; }

    public int TotalRegistros { get; }

    public int Pagina { get; }

    public int TamanoPagina { get; }

    public int TotalPaginas => TotalRegistros == 0 ? 1 : (int)Math.Ceiling(TotalRegistros / (double)TamanoPagina);

    public bool TieneAnterior => Pagina > 1;

    public bool TieneSiguiente => Pagina < TotalPaginas;

    public int PrimerRegistro => TotalRegistros == 0 ? 0 : ((Pagina - 1) * TamanoPagina) + 1;

    public int UltimoRegistro => Math.Min(Pagina * TamanoPagina, TotalRegistros);

    public static ResultadoPaginado<T> Vacio(int tamanoPagina = 25) =>
        new(Array.Empty<T>(), 0, 1, tamanoPagina);
}
