using Microsoft.EntityFrameworkCore;
using Papeleria.Business.Common;
using Papeleria.Business.Security;
using Papeleria.Data.Repositories;
using Papeleria.Domain.Entities;
using Papeleria.Domain.Exceptions;

namespace Papeleria.Business.Services.Catalogos;

/// <inheritdoc cref="IServicioCategorias" />
public class ServicioCategorias : ServicioCatalogoBase<Categoria>, IServicioCategorias
{
    public ServicioCategorias(IUnidadDeTrabajoFactory fabrica, IContextoSesion sesion)
        : base(fabrica, sesion) { }

    protected override string NombreEntidad => "la categoría";

    protected override string ObtenerNombre(Categoria entidad) => entidad.Nombre;

    protected override void EstablecerNombre(Categoria destino, Categoria origen)
    {
        destino.Nombre = Texto.Normalizar(origen.Nombre);
        destino.Descripcion = Texto.NormalizarOpcional(origen.Descripcion);
    }

    protected override Task<int> ContarUsosAsync(IUnidadDeTrabajo unidad, int id, CancellationToken ct) =>
        unidad.Contexto.Productos.CountAsync(p => p.CategoriaId == id, ct);
}

/// <inheritdoc cref="IServicioMarcas" />
public class ServicioMarcas : ServicioCatalogoBase<Marca>, IServicioMarcas
{
    public ServicioMarcas(IUnidadDeTrabajoFactory fabrica, IContextoSesion sesion)
        : base(fabrica, sesion) { }

    protected override string NombreEntidad => "la marca";

    protected override string ObtenerNombre(Marca entidad) => entidad.Nombre;

    protected override void EstablecerNombre(Marca destino, Marca origen)
    {
        destino.Nombre = Texto.Normalizar(origen.Nombre);
        destino.Descripcion = Texto.NormalizarOpcional(origen.Descripcion);
    }

    protected override Task<int> ContarUsosAsync(IUnidadDeTrabajo unidad, int id, CancellationToken ct) =>
        unidad.Contexto.Productos.CountAsync(p => p.MarcaId == id, ct);
}

/// <inheritdoc cref="IServicioUnidadesMedida" />
public class ServicioUnidadesMedida : ServicioCatalogoBase<UnidadMedida>, IServicioUnidadesMedida
{
    public ServicioUnidadesMedida(IUnidadDeTrabajoFactory fabrica, IContextoSesion sesion)
        : base(fabrica, sesion) { }

    protected override string NombreEntidad => "la unidad de medida";

    protected override string ObtenerNombre(UnidadMedida entidad) => entidad.Nombre;

    protected override void EstablecerNombre(UnidadMedida destino, UnidadMedida origen)
    {
        destino.Nombre = Texto.Normalizar(origen.Nombre);
        destino.Abreviatura = Texto.Normalizar(origen.Abreviatura).ToUpperInvariant();
        destino.Descripcion = Texto.NormalizarOpcional(origen.Descripcion);
    }

    protected override Task<int> ContarUsosAsync(IUnidadDeTrabajo unidad, int id, CancellationToken ct) =>
        unidad.Contexto.Productos.CountAsync(p => p.UnidadMedidaId == id, ct);

    public override Task<UnidadMedida> CrearAsync(UnidadMedida entidad, CancellationToken ct = default)
    {
        ValidarAbreviatura(entidad);
        entidad.Nombre = Texto.Normalizar(entidad.Nombre);
        entidad.Abreviatura = Texto.Normalizar(entidad.Abreviatura).ToUpperInvariant();
        return base.CrearAsync(entidad, ct);
    }

    public override Task ActualizarAsync(UnidadMedida entidad, CancellationToken ct = default)
    {
        ValidarAbreviatura(entidad);
        return base.ActualizarAsync(entidad, ct);
    }

    private static void ValidarAbreviatura(UnidadMedida entidad)
    {
        if (string.IsNullOrWhiteSpace(entidad.Abreviatura))
        {
            throw new NegocioException("Escriba la abreviatura de la unidad de medida (por ejemplo, UND).");
        }

        if (entidad.Abreviatura.Trim().Length > 10)
        {
            throw new NegocioException("La abreviatura no puede superar los 10 caracteres.");
        }
    }
}
