using Papeleria.Domain.Common;
using Papeleria.Domain.Entities;

namespace Papeleria.Business.Services.Catalogos;

/// <summary>
/// Contrato común de los catálogos simples (categorías, marcas y unidades de medida):
/// todos son listas de nombre único que pueden desactivarse en lugar de borrarse.
/// </summary>
public interface IServicioCatalogo<T> where T : EntidadBase, IActivable
{
    Task<List<T>> ListarAsync(bool soloActivos = false, CancellationToken ct = default);

    Task<T?> ObtenerAsync(int id, CancellationToken ct = default);

    Task<T> CrearAsync(T entidad, CancellationToken ct = default);

    Task ActualizarAsync(T entidad, CancellationToken ct = default);

    /// <summary>Elimina el registro; si ya se usa en productos, lo desactiva y avisa.</summary>
    Task EliminarAsync(int id, CancellationToken ct = default);
}

public interface IServicioCategorias : IServicioCatalogo<Categoria>;

public interface IServicioMarcas : IServicioCatalogo<Marca>;

public interface IServicioUnidadesMedida : IServicioCatalogo<UnidadMedida>;
