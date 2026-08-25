using Microsoft.EntityFrameworkCore;
using Papeleria.Business.Dtos;
using Papeleria.Data.Repositories;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Enums;

namespace Papeleria.Business.Services;

/// <inheritdoc cref="IServicioAyuda" />
public class ServicioAyuda : IServicioAyuda
{
    private readonly IUnidadDeTrabajoFactory _fabrica;
    private readonly IServicioAutenticacion _autenticacion;

    public ServicioAyuda(IUnidadDeTrabajoFactory fabrica, IServicioAutenticacion autenticacion)
    {
        _fabrica = fabrica;
        _autenticacion = autenticacion;
    }

    public async Task<ProgresoTutorialDto> ObtenerProgresoAsync(CancellationToken ct = default)
    {
        await using var unidad = _fabrica.Crear();

        // Se consulta el estado real del negocio: el tutorial no marca casillas a
        // mano, comprueba qué hay hecho de verdad.
        var empresa = await unidad.Contexto.Configuraciones
            .AsNoTracking()
            .Where(c => c.Clave == ClavesConfiguracion.EmpresaNombre)
            .Select(c => c.Valor)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        var carpetaBackup = await unidad.Contexto.Configuraciones
            .AsNoTracking()
            .Where(c => c.Clave == ClavesConfiguracion.BackupCarpeta)
            .Select(c => c.Valor)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        var categorias = await unidad.Contexto.Categorias.CountAsync(ct).ConfigureAwait(false);

        var productos = await unidad.Contexto.Productos
            .CountAsync(p => p.Tipo == TipoProducto.Producto, ct).ConfigureAwait(false);

        var servicios = await unidad.Contexto.Productos
            .CountAsync(p => p.Tipo == TipoProducto.Servicio, ct).ConfigureAwait(false);

        var proveedores = await unidad.Contexto.Proveedores.CountAsync(ct).ConfigureAwait(false);
        var compras = await unidad.Contexto.Compras.CountAsync(ct).ConfigureAwait(false);
        var ventas = await unidad.Contexto.Ventas.CountAsync(ct).ConfigureAwait(false);

        var huboCaja = await unidad.Contexto.CajaSesiones.AnyAsync(ct).ConfigureAwait(false);

        var contrasenaDeFabrica = await _autenticacion
            .UsaContrasenaPorDefectoAsync(SembradorDatos_UsuarioAdministrador, ct)
            .ConfigureAwait(false);

        var pasos = new List<PasoTutorialDto>
        {
            new()
            {
                Numero = 1,
                Titulo = "Ponga los datos de su papelería",
                Descripcion = "Nombre, NIT, teléfono y dirección del negocio.",
                PorQue = "Es lo que sale impreso en cada factura y en cada reporte que entregue.",
                Icono = "StoreOutline",
                Modulo = Modulos.Configuracion,
                Completado = !string.IsNullOrWhiteSpace(empresa) && empresa != "Mi Papelería",
                Detalle = string.IsNullOrWhiteSpace(empresa) ? null : $"Actualmente: {empresa}"
            },
            new()
            {
                Numero = 2,
                Titulo = "Cambie la contraseña de fábrica",
                Descripcion = "El usuario «admin» viene con una contraseña que todo el mundo conoce.",
                PorQue = "Su equipo queda en el mostrador: cualquiera podría entrar y ver sus ganancias.",
                Icono = "LockReset",
                Modulo = Modulos.Usuarios,
                Completado = !contrasenaDeFabrica
            },
            new()
            {
                Numero = 3,
                Titulo = "Cree sus categorías y unidades",
                Descripcion = "Cuadernos, escritura, oficina… y las unidades con las que vende.",
                PorQue = "Sin categorías no puede filtrar el inventario ni saber qué línea le deja más.",
                Icono = "ShapeOutline",
                Modulo = Modulos.Catalogos,
                Completado = categorias > 0,
                Detalle = categorias > 0 ? $"{categorias} categoría(s) creada(s)" : null
            },
            new()
            {
                Numero = 4,
                Titulo = "Registre sus productos",
                Descripcion = "Código, precio y existencias de lo que vende.",
                PorQue = "Es el catálogo del que se surte el punto de venta.",
                Icono = "PackageVariantClosed",
                Modulo = Modulos.Productos,
                Completado = productos > 0,
                Detalle = productos > 0 ? $"{productos} producto(s) registrado(s)" : null
            },
            new()
            {
                Numero = 5,
                Titulo = "Registre sus servicios",
                Descripcion = "Fotocopias, impresiones, anillado o plastificado.",
                PorQue = "Se cobran como cualquier producto pero sin descontar existencias.",
                Icono = "Printer",
                Modulo = Modulos.Productos,
                Completado = servicios > 0,
                EsOpcional = true,
                Detalle = servicios > 0 ? $"{servicios} servicio(s) registrado(s)" : null
            },
            new()
            {
                Numero = 6,
                Titulo = "Anote sus proveedores",
                Descripcion = "A quién le compra la mercancía.",
                PorQue = "Toda compra se registra a nombre de un proveedor.",
                Icono = "TruckDeliveryOutline",
                Modulo = Modulos.Proveedores,
                Completado = proveedores > 0,
                EsOpcional = true,
                Detalle = proveedores > 0 ? $"{proveedores} proveedor(es)" : null
            },
            new()
            {
                Numero = 7,
                Titulo = "Registre su primera compra",
                Descripcion = "Así entra la mercancía al inventario con su costo.",
                PorQue = "Sin compras el costo queda en cero y la utilidad que vea será falsa.",
                Icono = "TruckOutline",
                Modulo = Modulos.Compras,
                Completado = compras > 0,
                EsOpcional = true,
                Detalle = compras > 0 ? $"{compras} compra(s) registrada(s)" : null
            },
            new()
            {
                Numero = 8,
                Titulo = "Abra la caja",
                Descripcion = "Indique con cuánto dinero empieza el turno.",
                PorQue = "Sin caja abierta el punto de venta no deja facturar.",
                Icono = "CashMultiple",
                Modulo = Modulos.Caja,
                Completado = huboCaja
            },
            new()
            {
                Numero = 9,
                Titulo = "Haga su primera venta",
                Descripcion = "Escanee o busque un producto, cobre e imprima el recibo.",
                PorQue = "Es la prueba de que todo lo anterior quedó bien montado.",
                Icono = "CashRegister",
                Modulo = Modulos.Ventas,
                Completado = ventas > 0,
                Detalle = ventas > 0 ? $"{ventas} venta(s) registrada(s)" : null
            },
            new()
            {
                Numero = 10,
                Titulo = "Elija dónde guardar los respaldos",
                Descripcion = "Preferiblemente una memoria USB o un disco aparte.",
                PorQue = "Si se daña este computador, el respaldo en el mismo disco se pierde con él.",
                Icono = "DatabaseArrowDown",
                Modulo = Modulos.Configuracion,
                Completado = !string.IsNullOrWhiteSpace(carpetaBackup)
            }
        };

        return new ProgresoTutorialDto { Pasos = pasos };
    }

    /// <summary>Nombre del administrador sembrado; se repite aquí para no atar capas.</summary>
    private const string SembradorDatos_UsuarioAdministrador = "admin";
}
