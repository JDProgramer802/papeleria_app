using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Papeleria.Business.Dtos;
using Papeleria.Business.Services;
using Papeleria.Business.Security;
using Papeleria.Data;
using Papeleria.Domain.Constants;
using Papeleria.Domain.Enums;
using Papeleria.Domain.Exceptions;

namespace Papeleria.Pruebas;

public static partial class Programa
{
    private static async Task ComprobarMigracionGastosAsync(string archivo)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={archivo}").Options;
        await using var db = new AppDbContext(opciones);
        await db.GetService<IMigrator>().MigrateAsync("20260905002656_Cotizaciones");
        // FechaCreacion es obligatoria en la tabla: sin ella SQLite rechaza la fila y la prueba nunca llega a migrar.
        await db.Database.ExecuteSqlRawAsync("INSERT INTO Configuraciones (Clave, Valor, FechaCreacion) VALUES ('prueba.conservar', 'Datos anteriores', '2026-09-05 00:00:00')");
        await db.Database.MigrateAsync();
        Verificar("la actualización conserva los datos anteriores", await db.Configuraciones.AnyAsync(c => c.Clave == "prueba.conservar" && c.Valor == "Datos anteriores"));
        Verificar("la actualización crea Gastos sin registros inventados", await db.Gastos.CountAsync() == 0);
        Verificar("el modelo coincide con las migraciones", !db.Database.HasPendingModelChanges());
    }

    private static async Task ComprobarGastosAsync(IServiceProvider proveedor)
    {
        Console.WriteLine("Gastos operativos");
        var gastos = proveedor.GetRequiredService<IServicioGastos>();
        var caja = proveedor.GetRequiredService<IServicioCaja>();
        var contexto = proveedor.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var hoy = DateTime.Today;
        var filtro = new FiltroGastos(hoy.AddMonths(-1), hoy);
        var solicitud = new SolicitudGasto(hoy, "Pago de internet", "Servicios públicos", 10000, MetodoPago.Efectivo, "Operador", "REC-001");

        async Task Rechaza(string nombre, Func<Task> operacion)
        {
            try { await operacion(); Verificar(nombre, false, "La operación fue aceptada."); }
            catch (NegocioException) { Verificar(nombre, true); }
        }

        await Rechaza("un gasto en efectivo requiere caja abierta", async () => await gastos.RegistrarAsync(solicitud));
        foreach (var monto in new[] { 0m, -100m, 0.001m })
            await Rechaza($"no se admite un gasto de {monto}", async () => await gastos.RegistrarAsync(solicitud with { Monto = monto }));
        await Rechaza("no se admiten fechas futuras", async () => await gastos.RegistrarAsync(solicitud with { Fecha = hoy.AddDays(1) }));
        await Rechaza("no se admiten pagos mixtos", async () => await gastos.RegistrarAsync(solicitud with { MetodoPago = MetodoPago.Mixto }));
        await Rechaza("no se admite concepto vacío", async () => await gastos.RegistrarAsync(solicitud with { Concepto = " " }));
        await Rechaza("no se admite rango invertido", async () => await gastos.BuscarAsync(new(hoy, hoy.AddDays(-1))));

        var digital = await gastos.RegistrarAsync(solicitud with { MetodoPago = MetodoPago.Nequi });
        await using (var db = await contexto.CreateDbContextAsync())
            Verificar("un pago digital se registra sin movimiento de caja", (await db.Gastos.FindAsync(digital))!.MovimientoCajaId == null);

        var turno = await caja.AbrirAsync(50000, "Prueba gastos");
        var efectivo = await gastos.RegistrarAsync(solicitud);
        Verificar("el gasto descuenta el efectivo una sola vez", (await caja.CalcularArqueoAsync(turno.Id)).MontoEsperado == 40000);
        await Rechaza("no se gasta más del saldo disponible", async () => await gastos.RegistrarAsync(solicitud with { Monto = 40001 }));
        Verificar("un gasto rechazado no deja registro parcial", (await gastos.BuscarAsync(filtro)).Count == 2);
        var tarjeta = await gastos.RegistrarAsync(solicitud with { MetodoPago = MetodoPago.Tarjeta, Fecha = hoy.AddDays(-1) });
        Verificar("tarjeta no modifica el cajón", (await caja.CalcularArqueoAsync(turno.Id)).MontoEsperado == 40000);
        Verificar("la búsqueda por referencia incluye el último día", (await gastos.BuscarAsync(new(hoy, hoy, "rec-001"))).Count == 2);
        Verificar("el filtro de categoría se aplica", (await gastos.BuscarAsync(filtro with { Categoria = "Arriendo" })).Count == 0);

        await caja.CerrarAsync(turno.Id, 40000, "Cierre de prueba");
        await Rechaza("no se reintegra efectivo sin caja abierta", () => gastos.AnularAsync(efectivo, "Reembolso"));
        var nuevoTurno = await caja.AbrirAsync(20000, "Nuevo turno");
        await gastos.AnularAsync(efectivo, "Reembolso del proveedor");
        Verificar("el reintegro entra al turno actual", (await caja.CalcularArqueoAsync(nuevoTurno.Id)).MontoEsperado == 30000);
        await Rechaza("no se permite anulación doble", () => gastos.AnularAsync(efectivo, "Duplicado"));
        Verificar("el intento duplicado no reintegra dos veces", (await caja.CalcularArqueoAsync(nuevoTurno.Id)).MontoEsperado == 30000);
        Verificar("los anulados se excluyen por defecto", (await gastos.BuscarAsync(filtro)).All(g => g.Id != efectivo));
        Verificar("los anulados conservan el motivo", (await gastos.BuscarAsync(filtro with { IncluirAnulados = true })).Single(g => g.Id == efectivo).MotivoAnulacion == "Reembolso del proveedor");
        await gastos.AnularAsync(digital, "Reembolso digital");
        Verificar("anular un pago digital no afecta caja", (await caja.CalcularArqueoAsync(nuevoTurno.Id)).MontoEsperado == 30000);
        await using (var db = await contexto.CreateDbContextAsync())
        {
            var g = await db.Gastos.FindAsync(efectivo);
            Verificar("se conservan autor y movimientos de la anulación", g!.FechaAnulacion != null && g.UsuarioAnulacionId != null && g.MovimientoCajaId != null && g.MovimientoReversionId != null);
            Verificar("el cierre anterior no cambia", (await db.CajaSesiones.FindAsync(turno.Id))!.MontoEsperado == 40000);
        }
        var sesion = proveedor.GetRequiredService<IContextoSesion>();
        var administrador = sesion.Usuario!;
        sesion.Iniciar(new UsuarioSesion { Id = administrador.Id, NombreUsuario = "restringido", NombreCompleto = "Prueba", Rol = RolUsuario.Bodega, Permisos = new Dictionary<string, PermisoEfectivo>() });
        try
        {
            await Rechaza("se exige permiso de lectura", async () => await gastos.BuscarAsync(filtro));
            await Rechaza("se exige permiso de creación", async () => await gastos.RegistrarAsync(solicitud));
            await Rechaza("se exige permiso de anulación", () => gastos.AnularAsync(tarjeta, "Prueba"));
        }
        finally { sesion.Iniciar(administrador); }
        await caja.CerrarAsync(nuevoTurno.Id, 30000, "Fin prueba gastos");
    }
}
