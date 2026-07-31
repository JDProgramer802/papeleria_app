using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Papeleria.Business.Common;
using Papeleria.Business.Dtos;
using Papeleria.Data.Repositories;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Entities;

namespace Papeleria.Business.Services;

/// <inheritdoc cref="IServicioConfiguracion" />
public class ServicioConfiguracion : IServicioConfiguracion
{
    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ServicioConfiguracion(IUnidadDeTrabajoFactory fabrica) => _fabrica = fabrica;

    public event EventHandler? ConfiguracionCambiada;

    public async Task CargarAsync(CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        var valores = await unidad.Contexto.Configuraciones
            .AsNoTracking()
            .Select(c => new { c.Clave, c.Valor })
            .ToListAsync(ct).ConfigureAwait(false);

        _cache.Clear();

        foreach (var valor in valores)
        {
            _cache[valor.Clave] = valor.Valor;
        }
    }

    public string ObtenerTexto(string clave, string valorPorDefecto = "") =>
        _cache.TryGetValue(clave, out var valor) && !string.IsNullOrWhiteSpace(valor)
            ? valor
            : valorPorDefecto;

    public int ObtenerEntero(string clave, int valorPorDefecto = 0) =>
        int.TryParse(ObtenerTexto(clave), NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : valorPorDefecto;

    public decimal ObtenerDecimal(string clave, decimal valorPorDefecto = 0) =>
        decimal.TryParse(ObtenerTexto(clave), NumberStyles.Any, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : valorPorDefecto;

    public bool ObtenerBooleano(string clave, bool valorPorDefecto = false) =>
        bool.TryParse(ObtenerTexto(clave), out var valor) ? valor : valorPorDefecto;

    public DateTime? ObtenerFecha(string clave) =>
        DateTime.TryParse(ObtenerTexto(clave), CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var valor)
            ? valor
            : null;

    public Task GuardarAsync(string clave, string? valor, CancellationToken ct = default) =>
        GuardarVariosAsync(new Dictionary<string, string?> { [clave] = valor }, ct);

    public async Task GuardarVariosAsync(IReadOnlyDictionary<string, string?> valores, CancellationToken ct = default)
    {
        if (valores.Count == 0)
        {
            return;
        }

        await using var unidad = _fabrica.Crear();
        var claves = valores.Keys.ToList();

        var existentes = await unidad.Contexto.Configuraciones
            .Where(c => claves.Contains(c.Clave))
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var (clave, valor) in valores)
        {
            var registro = existentes.FirstOrDefault(
                c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));

            if (registro is null)
            {
                unidad.Contexto.Configuraciones.Add(new Configuracion { Clave = clave, Valor = valor });
            }
            else
            {
                registro.Valor = valor;
            }

            _cache[clave] = valor;
        }

        await unidad.GuardarCambiosAsync(ct).ConfigureAwait(false);
        ConfiguracionCambiada?.Invoke(this, EventArgs.Empty);
    }

    public DatosEmpresa ObtenerEmpresa() => new()
    {
        Nombre = ObtenerTexto(ClavesConfiguracion.EmpresaNombre, "Mi Papelería"),
        Nit = ObtenerTexto(ClavesConfiguracion.EmpresaNit),
        Direccion = ObtenerTexto(ClavesConfiguracion.EmpresaDireccion),
        Telefono = ObtenerTexto(ClavesConfiguracion.EmpresaTelefono),
        Correo = ObtenerTexto(ClavesConfiguracion.EmpresaCorreo),
        Ciudad = ObtenerTexto(ClavesConfiguracion.EmpresaCiudad),
        Eslogan = ObtenerTexto(ClavesConfiguracion.EmpresaEslogan),
        LogoPath = ObtenerTexto(ClavesConfiguracion.EmpresaLogo),
        Resolucion = ObtenerTexto(ClavesConfiguracion.FacturaResolucion),
        PieFactura = ObtenerTexto(ClavesConfiguracion.FacturaPieDePagina, "¡Gracias por su compra!"),
        MonedaSimbolo = ObtenerTexto(ClavesConfiguracion.MonedaSimbolo, "$"),
        MonedaCodigo = ObtenerTexto(ClavesConfiguracion.MonedaCodigo, "COP"),
        DecimalesMoneda = ObtenerEntero(ClavesConfiguracion.DecimalesMoneda),
        IvaPorDefecto = ObtenerDecimal(ClavesConfiguracion.ImpuestoPorDefecto, 19m)
    };

    public Task GuardarEmpresaAsync(DatosEmpresa empresa, CancellationToken ct = default)
    {
        var valores = new Dictionary<string, string?>
        {
            [ClavesConfiguracion.EmpresaNombre] = Texto.Normalizar(empresa.Nombre),
            [ClavesConfiguracion.EmpresaNit] = Texto.Normalizar(empresa.Nit),
            [ClavesConfiguracion.EmpresaDireccion] = Texto.Normalizar(empresa.Direccion),
            [ClavesConfiguracion.EmpresaTelefono] = Texto.Normalizar(empresa.Telefono),
            [ClavesConfiguracion.EmpresaCorreo] = Texto.Normalizar(empresa.Correo),
            [ClavesConfiguracion.EmpresaCiudad] = Texto.Normalizar(empresa.Ciudad),
            [ClavesConfiguracion.EmpresaEslogan] = Texto.Normalizar(empresa.Eslogan),
            [ClavesConfiguracion.EmpresaLogo] = Texto.Normalizar(empresa.LogoPath),
            [ClavesConfiguracion.FacturaResolucion] = Texto.Normalizar(empresa.Resolucion),
            [ClavesConfiguracion.FacturaPieDePagina] = Texto.Normalizar(empresa.PieFactura),
            [ClavesConfiguracion.MonedaSimbolo] = Texto.Normalizar(empresa.MonedaSimbolo),
            [ClavesConfiguracion.MonedaCodigo] = Texto.Normalizar(empresa.MonedaCodigo),
            [ClavesConfiguracion.DecimalesMoneda] = empresa.DecimalesMoneda.ToString(CultureInfo.InvariantCulture),
            [ClavesConfiguracion.ImpuestoPorDefecto] = empresa.IvaPorDefecto.ToString(CultureInfo.InvariantCulture)
        };

        return GuardarVariosAsync(valores, ct);
    }

    public async Task<string> ReservarConsecutivoAsync(
        IUnidadDeTrabajo unidad, string clavePrefijo, string claveConsecutivo, CancellationToken ct = default)
    {
        var registros = await unidad.Contexto.Configuraciones
            .Where(c => c.Clave == clavePrefijo || c.Clave == claveConsecutivo)
            .ToListAsync(ct).ConfigureAwait(false);

        var registroPrefijo = registros.FirstOrDefault(c => c.Clave == clavePrefijo);
        var registroConsecutivo = registros.FirstOrDefault(c => c.Clave == claveConsecutivo);

        if (registroConsecutivo is null)
        {
            registroConsecutivo = new Configuracion { Clave = claveConsecutivo, Valor = "0" };
            unidad.Contexto.Configuraciones.Add(registroConsecutivo);
        }

        var actual = int.TryParse(registroConsecutivo.Valor, out var numero) ? numero : 0;
        var siguiente = actual + 1;

        registroConsecutivo.Valor = siguiente.ToString(CultureInfo.InvariantCulture);
        _cache[claveConsecutivo] = registroConsecutivo.Valor;

        return Texto.Consecutivo(registroPrefijo?.Valor, siguiente);
    }
}
