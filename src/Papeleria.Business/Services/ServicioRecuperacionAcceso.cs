using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Papeleria.Business.Security;
using Papeleria.Data.Repositories;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Exceptions;
using Papeleria.Domain.Security;

namespace Papeleria.Business.Services;

/// <inheritdoc cref="IServicioRecuperacionAcceso" />
public class ServicioRecuperacionAcceso : IServicioRecuperacionAcceso
{
    /// <summary>
    /// Sin I, O, 0 ni 1: escrito a mano en un papel, se confunden entre sí. Son 32
    /// símbolos, cinco bits cada uno.
    /// </summary>
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Dieciséis símbolos son 80 bits: no se adivina ni probando de a millones.</summary>
    private const int LongitudCodigo = 16;

    private const int IntentosPermitidos = 5;

    private static readonly TimeSpan Bloqueo = TimeSpan.FromMinutes(15);

    /// <summary>Mismo mensaje para todo fallo: no se revela si el usuario existe ni qué parte falló.</summary>
    private const string MensajeRechazo =
        "El usuario o el código de recuperación no son correctos.";

    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IServicioHash _hash;
    private readonly IServicioConfiguracion _configuracion;
    private readonly IContextoSesion _sesion;
    private readonly ILogger<ServicioRecuperacionAcceso> _log;

    /// <summary>Intentos fallidos por usuario. En memoria: reiniciar el programa los borra, y está bien.</summary>
    private readonly ConcurrentDictionary<string, (int Fallos, DateTime? BloqueadoHasta)> _intentos =
        new(StringComparer.OrdinalIgnoreCase);

    public ServicioRecuperacionAcceso(
        IUnidadDeTrabajoFactory fabrica,
        IServicioHash hash,
        IServicioConfiguracion configuracion,
        IContextoSesion sesion,
        ILogger<ServicioRecuperacionAcceso> log)
    {
        _fabrica = fabrica;
        _hash = hash;
        _configuracion = configuracion;
        _sesion = sesion;
        _log = log;
    }

    public async Task<string> GenerarCodigoAsync(CancellationToken ct = default)
    {
        var usuarioId = _sesion.UsuarioIdRequerido;
        var codigo = await GuardarCodigoNuevoAsync(usuarioId, ct).ConfigureAwait(false);

        _log.LogInformation("{Usuario} generó un código de recuperación nuevo", _sesion.Usuario?.NombreUsuario);
        return codigo;
    }

    public DateTime? FechaCodigo(int usuarioId) =>
        string.IsNullOrWhiteSpace(_configuracion.ObtenerTexto(ClaveHash(usuarioId)))
            ? null
            : _configuracion.ObtenerFecha(ClaveFecha(usuarioId));

    public async Task<string> RestablecerConCodigoAsync(
        string nombreUsuario, string codigo, string contrasenaNueva, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nombreUsuario) || string.IsNullOrWhiteSpace(codigo))
        {
            throw new NegocioException("Escriba su usuario y su código de recuperación.");
        }

        nombreUsuario = nombreUsuario.Trim();
        ComprobarBloqueo(nombreUsuario);

        // La contraseña se revisa antes que el código: un error de tecleo en la
        // contraseña nueva no debe contar como intento fallido.
        ServicioAutenticacion.ValidarFortaleza(contrasenaNueva);

        await using var unidad = _fabrica.Crear();

        var usuario = await unidad.Contexto.Usuarios
            .FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario, ct)
            .ConfigureAwait(false);

        var hashGuardado = usuario is null ? string.Empty : _configuracion.ObtenerTexto(ClaveHash(usuario.Id));

        if (usuario is null || !usuario.Activo || string.IsNullOrWhiteSpace(hashGuardado)
            || !_hash.Verificar(Normalizar(codigo), hashGuardado))
        {
            RegistrarFallo(nombreUsuario);
            throw new NegocioException(MensajeRechazo);
        }

        _intentos.TryRemove(nombreUsuario, out _);

        usuario.PasswordHash = _hash.Generar(contrasenaNueva);
        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);

        // El código usado queda gastado: se reemplaza en el acto por otro.
        var nuevo = await GuardarCodigoNuevoAsync(usuario.Id, ct).ConfigureAwait(false);

        _log.LogWarning("{Usuario} restableció su contraseña con el código de recuperación", usuario.NombreUsuario);
        return nuevo;
    }

    /// <summary>Genera, guarda el hash y devuelve el código formateado «ABCD-EFGH-JKLM-NPQR».</summary>
    private async Task<string> GuardarCodigoNuevoAsync(int usuarioId, CancellationToken ct)
    {
        var simbolos = RandomNumberGenerator.GetString(Alfabeto, LongitudCodigo);

        await _configuracion.GuardarVariosAsync(new Dictionary<string, string?>
        {
            [ClaveHash(usuarioId)] = _hash.Generar(simbolos),
            [ClaveFecha(usuarioId)] = DateTime.Now.ToString("s", CultureInfo.InvariantCulture)
        }, ct).ConfigureAwait(false);

        return string.Join("-", Enumerable.Range(0, LongitudCodigo / 4).Select(i => simbolos.Substring(i * 4, 4)));
    }

    /// <summary>Se acepta como lo escriba la persona: con o sin guiones, en minúscula, con espacios.</summary>
    private static string Normalizar(string codigo) =>
        new(codigo.ToUpperInvariant().Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());

    private void ComprobarBloqueo(string nombreUsuario)
    {
        if (_intentos.TryGetValue(nombreUsuario, out var estado)
            && estado.BloqueadoHasta is { } hasta && hasta > DateTime.Now)
        {
            var minutos = Math.Max(1, (int)Math.Ceiling((hasta - DateTime.Now).TotalMinutes));

            throw new NegocioException(
                $"Demasiados intentos fallidos. Espere {minutos} minuto{(minutos == 1 ? "" : "s")} " +
                "y vuelva a intentarlo.");
        }
    }

    private void RegistrarFallo(string nombreUsuario)
    {
        _log.LogWarning("Intento fallido de recuperar la contraseña de {Usuario}", nombreUsuario);

        _intentos.AddOrUpdate(
            nombreUsuario,
            _ => (1, null),
            (_, estado) =>
            {
                var fallos = estado.BloqueadoHasta is { } hasta && hasta <= DateTime.Now ? 1 : estado.Fallos + 1;
                return fallos >= IntentosPermitidos ? (0, DateTime.Now + Bloqueo) : (fallos, null);
            });
    }

    private static string ClaveHash(int usuarioId) => $"{ClavesConfiguracion.RecuperacionPrefijo}{usuarioId}.hash";

    private static string ClaveFecha(int usuarioId) => $"{ClavesConfiguracion.RecuperacionPrefijo}{usuarioId}.fecha";
}
