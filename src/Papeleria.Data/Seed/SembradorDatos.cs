using Microsoft.EntityFrameworkCore;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Entities;
using Papeleria.Domain.Enums;
using Papeleria.Domain.Security;

namespace Papeleria.Data.Seed;

/// <summary>
/// Inserta los datos mínimos que el sistema necesita para operar la primera vez.
/// Cada bloque comprueba antes de insertar, por lo que puede ejecutarse en cada arranque.
/// </summary>
public class SembradorDatos
{
    /// <summary>Credenciales del administrador creado en la primera ejecución.</summary>
    public const string UsuarioAdministrador = "admin";
    public const string ContrasenaAdministradorPorDefecto = "Admin123*";

    private readonly IServicioHash _hash;

    public SembradorDatos(IServicioHash hash) => _hash = hash;

    public async Task SembrarAsync(AppDbContext contexto, CancellationToken ct = default)
    {
        await SembrarUsuarioAdministradorAsync(contexto, ct).ConfigureAwait(false);
        await SembrarPermisosAsync(contexto, ct).ConfigureAwait(false);
        await SembrarConfiguracionAsync(contexto, ct).ConfigureAwait(false);
        await SembrarUnidadesAsync(contexto, ct).ConfigureAwait(false);
        await SembrarCategoriasAsync(contexto, ct).ConfigureAwait(false);
        await SembrarMarcasAsync(contexto, ct).ConfigureAwait(false);
        await SembrarClientePorDefectoAsync(contexto, ct).ConfigureAwait(false);
    }

    private async Task SembrarUsuarioAdministradorAsync(AppDbContext contexto, CancellationToken ct)
    {
        if (await contexto.Usuarios.AnyAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        contexto.Usuarios.Add(new Usuario
        {
            NombreUsuario = UsuarioAdministrador,
            NombreCompleto = "Administrador del sistema",
            PasswordHash = _hash.Generar(ContrasenaAdministradorPorDefecto),
            Rol = RolUsuario.Administrador,
            Activo = true,
            EsProtegido = true
        });

        await contexto.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static async Task SembrarPermisosAsync(AppDbContext contexto, CancellationToken ct)
    {
        var existentes = await contexto.Permisos
            .Select(p => new { p.Rol, p.Modulo })
            .ToListAsync(ct).ConfigureAwait(false);

        var yaCreados = existentes.Select(e => (e.Rol, e.Modulo)).ToHashSet();
        var nuevos = new List<PermisoRol>();

        foreach (var rol in Enum.GetValues<RolUsuario>())
        {
            foreach (var modulo in Modulos.Todos)
            {
                if (yaCreados.Contains((rol, modulo)))
                {
                    continue;
                }

                nuevos.Add(PermisoPorDefecto(rol, modulo));
            }
        }

        if (nuevos.Count == 0)
        {
            return;
        }

        contexto.Permisos.AddRange(nuevos);
        await contexto.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Matriz de permisos inicial. El administrador puede modificarla después desde
    /// el módulo de usuarios sin tocar el código.
    /// </summary>
    private static PermisoRol PermisoPorDefecto(RolUsuario rol, string modulo)
    {
        var permiso = new PermisoRol { Rol = rol, Modulo = modulo };

        if (rol == RolUsuario.Administrador)
        {
            permiso.PuedeVer = permiso.PuedeCrear = permiso.PuedeEditar = permiso.PuedeEliminar = true;
            return permiso;
        }

        if (rol == RolUsuario.Cajero)
        {
            switch (modulo)
            {
                case Modulos.Dashboard:
                case Modulos.Productos:
                case Modulos.Inventario:
                case Modulos.Kardex:
                case Modulos.Reportes:
                    permiso.PuedeVer = true;
                    break;

                case Modulos.Clientes:
                    permiso.PuedeVer = permiso.PuedeCrear = permiso.PuedeEditar = true;
                    break;

                case Modulos.Ventas:
                case Modulos.Caja:
                    permiso.PuedeVer = permiso.PuedeCrear = permiso.PuedeEditar = true;
                    break;

                // Consultar y reimprimir facturas pasadas; anular sigue siendo del administrador.
                case Modulos.HistorialVentas:
                    permiso.PuedeVer = true;
                    break;

                // Consultar la deuda y recibir abonos en el mostrador.
                case Modulos.Cartera:
                    permiso.PuedeVer = permiso.PuedeCrear = true;
                    break;
            }

            return permiso;
        }

        // Bodega: gestiona catálogo, compras e inventario, sin acceso a caja ni ventas.
        switch (modulo)
        {
            case Modulos.Dashboard:
            case Modulos.Kardex:
            case Modulos.Reportes:
                permiso.PuedeVer = true;
                break;

            case Modulos.Productos:
            case Modulos.Catalogos:
            case Modulos.Proveedores:
            case Modulos.Inventario:
                permiso.PuedeVer = permiso.PuedeCrear = permiso.PuedeEditar = true;
                break;

            case Modulos.Compras:
                permiso.PuedeVer = permiso.PuedeCrear = true;
                break;
        }

        return permiso;
    }

    private static async Task SembrarConfiguracionAsync(AppDbContext contexto, CancellationToken ct)
    {
        var valoresPorDefecto = new (string Clave, string Valor, string Descripcion)[]
        {
            (ClavesConfiguracion.EmpresaNombre, "Mi Papelería", "Nombre comercial que aparece en facturas y reportes"),
            (ClavesConfiguracion.EmpresaNit, "", "NIT o documento de la empresa"),
            (ClavesConfiguracion.EmpresaDireccion, "", "Dirección de la empresa"),
            (ClavesConfiguracion.EmpresaTelefono, "", "Teléfono de contacto"),
            (ClavesConfiguracion.EmpresaCorreo, "", "Correo electrónico"),
            (ClavesConfiguracion.EmpresaCiudad, "", "Ciudad"),
            (ClavesConfiguracion.EmpresaLogo, "", "Ruta del logo de la empresa"),
            (ClavesConfiguracion.EmpresaEslogan, "", "Eslogan mostrado en la factura"),

            (ClavesConfiguracion.ImpuestoPorDefecto, "19", "Porcentaje de IVA sugerido para nuevos productos"),
            (ClavesConfiguracion.MonedaSimbolo, "$", "Símbolo de la moneda"),
            (ClavesConfiguracion.MonedaCodigo, "COP", "Código ISO de la moneda"),
            (ClavesConfiguracion.DecimalesMoneda, "0", "Decimales usados al mostrar importes"),

            (ClavesConfiguracion.FacturaPrefijo, "FV-", "Prefijo del consecutivo de facturas de venta"),
            (ClavesConfiguracion.FacturaConsecutivo, "0", "Último consecutivo de factura emitido"),
            (ClavesConfiguracion.FacturaResolucion, "", "Texto de resolución de facturación"),
            (ClavesConfiguracion.FacturaPieDePagina, "¡Gracias por su compra!", "Mensaje al pie del recibo"),
            (ClavesConfiguracion.CompraPrefijo, "CMP-", "Prefijo del consecutivo de compras"),
            (ClavesConfiguracion.CompraConsecutivo, "0", "Último consecutivo de compra registrado"),

            (ClavesConfiguracion.BackupCarpeta, "", "Carpeta destino de las copias de seguridad"),
            (ClavesConfiguracion.BackupAutomatico, "true", "Genera una copia automática al cerrar la aplicación"),
            (ClavesConfiguracion.BackupFrecuenciaDias, "1", "Días entre copias automáticas"),
            (ClavesConfiguracion.BackupUltimaFecha, "", "Fecha de la última copia realizada"),
            (ClavesConfiguracion.BackupRetencion, "30", "Cantidad de copias que se conservan"),

            (ClavesConfiguracion.ActualizacionesRepositorio, "JDProgramer802/papeleria_app",
                "Repositorio de GitHub del que se descargan las actualizaciones, con el formato usuario/repositorio"),
            (ClavesConfiguracion.ActualizacionesAutomaticas, "true",
                "Comprueba si hay una versión nueva al iniciar, como mucho una vez al día"),
            (ClavesConfiguracion.ActualizacionesUltimaComprobacion, "", "Fecha de la última comprobación"),
            (ClavesConfiguracion.ActualizacionesVersionOmitida, "", "Versión que el usuario pidió no volver a ver"),

            (ClavesConfiguracion.TemaOscuro, "false", "Preferencia de tema oscuro"),
            (ClavesConfiguracion.ColorPrimario, "#1565C0", "Color primario de la interfaz"),
            (ClavesConfiguracion.UltimoUsuario, "", "Último usuario que inició sesión"),
            (ClavesConfiguracion.RecordarUsuario, "true", "Recordar el último usuario en el login"),
            (ClavesConfiguracion.MenuColapsado, "false", "Estado del menú lateral")
        };

        var clavesExistentes = await contexto.Configuraciones
            .Select(c => c.Clave)
            .ToListAsync(ct).ConfigureAwait(false);

        var faltantes = valoresPorDefecto
            .Where(v => !clavesExistentes.Contains(v.Clave))
            .Select(v => new Configuracion { Clave = v.Clave, Valor = v.Valor, Descripcion = v.Descripcion })
            .ToList();

        if (faltantes.Count == 0)
        {
            return;
        }

        contexto.Configuraciones.AddRange(faltantes);
        await contexto.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static async Task SembrarUnidadesAsync(AppDbContext contexto, CancellationToken ct)
    {
        if (await contexto.UnidadesMedida.AnyAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        var unidades = new (string Nombre, string Abreviatura)[]
        {
            ("Unidad", "UND"), ("Caja", "CJA"), ("Paquete", "PQT"), ("Resma", "RSM"),
            ("Docena", "DOC"), ("Block", "BLK"), ("Rollo", "RLL"), ("Bolsa", "BLS"),
            ("Metro", "MTR"), ("Kilogramo", "KG"), ("Juego", "JGO"), ("Par", "PAR")
        };

        contexto.UnidadesMedida.AddRange(unidades.Select(u => new UnidadMedida
        {
            Nombre = u.Nombre,
            Abreviatura = u.Abreviatura,
            Activo = true
        }));

        await contexto.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static async Task SembrarCategoriasAsync(AppDbContext contexto, CancellationToken ct)
    {
        if (await contexto.Categorias.AnyAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        var categorias = new (string Nombre, string Descripcion)[]
        {
            ("Cuadernos y libretas", "Cuadernos cosidos, argollados y libretas"),
            ("Escritura", "Lápices, bolígrafos, marcadores y correctores"),
            ("Papelería general", "Resmas, papeles, sobres y cartulinas"),
            ("Arte y manualidades", "Pinturas, pinceles, plastilina y foamy"),
            ("Oficina", "Grapadoras, perforadoras, carpetas y archivadores"),
            ("Escolar", "Loncheras, maletines, reglas y compases"),
            ("Tecnología", "Memorias, cables, audífonos y accesorios"),
            ("Impresión", "Tintas, tóner y suministros de impresión"),
            ("Empaque", "Cintas, bolsas y material de empaque"),
            ("Aseo y cafetería", "Productos de limpieza y desechables")
        };

        contexto.Categorias.AddRange(categorias.Select(c => new Categoria
        {
            Nombre = c.Nombre,
            Descripcion = c.Descripcion,
            Activo = true
        }));

        await contexto.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static async Task SembrarMarcasAsync(AppDbContext contexto, CancellationToken ct)
    {
        if (await contexto.Marcas.AnyAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        var marcas = new[]
        {
            "Genérica", "Norma", "Faber-Castell", "Bic", "Pelikan", "Scribe",
            "Kimberly", "Ofixpress", "Studmark", "Primavera", "Papelco", "Nataly"
        };

        contexto.Marcas.AddRange(marcas.Select(m => new Marca { Nombre = m, Activo = true }));
        await contexto.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static async Task SembrarClientePorDefectoAsync(AppDbContext contexto, CancellationToken ct)
    {
        if (await contexto.Clientes.AnyAsync(c => c.EsProtegido, ct).ConfigureAwait(false))
        {
            return;
        }

        contexto.Clientes.Add(new Cliente
        {
            Nombre = "Consumidor final",
            TipoDocumento = TipoDocumento.SinIdentificacion,
            NumeroDocumento = "222222222222",
            Activo = true,
            EsProtegido = true,
            Observaciones = "Cliente por defecto del punto de venta. No puede eliminarse."
        });

        await contexto.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
