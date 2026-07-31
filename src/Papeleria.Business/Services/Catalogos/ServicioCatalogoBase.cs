using Microsoft.EntityFrameworkCore;
using Papeleria.Business.Common;
using Papeleria.Business.Security;
using Papeleria.Data.Repositories;
using Papeleria.Domain.Common;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Entities;
using Papeleria.Domain.Exceptions;

namespace Papeleria.Business.Services.Catalogos;

/// <summary>
/// Lógica compartida por los catálogos simples. Las clases derivadas solo describen
/// cómo se llama el catálogo, cómo se lee su nombre y cuándo está en uso.
/// </summary>
public abstract class ServicioCatalogoBase<T> : IServicioCatalogo<T> where T : EntidadBase, IActivable
{
    protected ServicioCatalogoBase(IUnidadDeTrabajoFactory fabrica, IContextoSesion sesion)
    {
        Fabrica = fabrica;
        Sesion = sesion;
    }

    protected IUnidadDeTrabajoFactory Fabrica { get; }

    protected IContextoSesion Sesion { get; }

    /// <summary>Nombre singular usado en los mensajes al usuario («la categoría»).</summary>
    protected abstract string NombreEntidad { get; }

    protected abstract string ObtenerNombre(T entidad);

    protected abstract void EstablecerNombre(T destino, T origen);

    /// <summary>Cantidad de productos que dependen del registro.</summary>
    protected abstract Task<int> ContarUsosAsync(IUnidadDeTrabajo unidad, int id, CancellationToken ct);

    public virtual async Task<List<T>> ListarAsync(bool soloActivos = false, CancellationToken ct = default)
    {
        Sesion.Exigir(Modulos.Catalogos, AccionPermiso.Ver);

        await using var unidad = Fabrica.Crear();

        var consulta = unidad.Contexto.Set<T>().AsNoTracking();

        if (soloActivos)
        {
            consulta = consulta.Where(e => e.Activo);
        }

        var lista = await consulta.ToListAsync(ct).ConfigureAwait(false);
        return lista.OrderBy(ObtenerNombre, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public virtual async Task<T?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        await using var unidad = Fabrica.Crear();
        return await unidad.Contexto.Set<T>().AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct).ConfigureAwait(false);
    }

    public virtual async Task<T> CrearAsync(T entidad, CancellationToken ct = default)
    {
        Sesion.Exigir(Modulos.Catalogos, AccionPermiso.Crear);
        Validar(entidad);

        await using var unidad = Fabrica.Crear();
        await ValidarNombreUnicoAsync(unidad, entidad, null, ct).ConfigureAwait(false);

        unidad.Contexto.Set<T>().Add(entidad);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

        return entidad;
    }

    public virtual async Task ActualizarAsync(T entidad, CancellationToken ct = default)
    {
        Sesion.Exigir(Modulos.Catalogos, AccionPermiso.Editar);
        Validar(entidad);

        await using var unidad = Fabrica.Crear();

        var actual = await unidad.Contexto.Set<T>().FirstOrDefaultAsync(e => e.Id == entidad.Id, ct)
                         .ConfigureAwait(false)
                     ?? throw new RegistroNoEncontradoException(NombreEntidad, entidad.Id);

        await ValidarNombreUnicoAsync(unidad, entidad, entidad.Id, ct).ConfigureAwait(false);

        EstablecerNombre(actual, entidad);
        actual.Activo = entidad.Activo;

        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
    }

    public virtual async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        Sesion.Exigir(Modulos.Catalogos, AccionPermiso.Eliminar);

        await using var unidad = Fabrica.Crear();

        var entidad = await unidad.Contexto.Set<T>().FirstOrDefaultAsync(e => e.Id == id, ct).ConfigureAwait(false)
                      ?? throw new RegistroNoEncontradoException(NombreEntidad, id);

        var usos = await ContarUsosAsync(unidad, id, ct).ConfigureAwait(false);

        if (usos > 0)
        {
            // Borrarlo rompería productos existentes: se desactiva para que deje de ofrecerse.
            entidad.Activo = false;
            await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

            throw new NegocioException(
                $"No se puede eliminar {NombreEntidad} «{ObtenerNombre(entidad)}» porque " +
                $"{(usos == 1 ? "1 producto la usa" : $"{usos} productos la usan")}. " +
                "Se desactivó para que no aparezca en nuevos registros.");
        }

        unidad.Contexto.Set<T>().Remove(entidad);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
    }

    private void Validar(T entidad)
    {
        if (string.IsNullOrWhiteSpace(ObtenerNombre(entidad)))
        {
            throw new NegocioException($"Escriba el nombre de {NombreEntidad}.");
        }
    }

    private async Task ValidarNombreUnicoAsync(IUnidadDeTrabajo unidad, T entidad, int? idExcluido, CancellationToken ct)
    {
        var nombre = Texto.Normalizar(ObtenerNombre(entidad));

        var existentes = await unidad.Contexto.Set<T>().AsNoTracking()
            .Where(e => idExcluido == null || e.Id != idExcluido)
            .ToListAsync(ct).ConfigureAwait(false);

        var duplicado = existentes.Any(
            e => string.Equals(Texto.Normalizar(ObtenerNombre(e)), nombre, StringComparison.CurrentCultureIgnoreCase));

        if (duplicado)
        {
            throw new NegocioException($"Ya existe {NombreEntidad} con el nombre «{nombre}».");
        }
    }
}
