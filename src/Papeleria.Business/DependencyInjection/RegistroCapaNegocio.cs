using Microsoft.Extensions.DependencyInjection;
using Papeleria.Business.Security;
using Papeleria.Business.Services;
using Papeleria.Business.Services.Catalogos;
using Papeleria.Data.DependencyInjection;
using Papeleria.Domain.Security;

namespace Papeleria.Business.DependencyInjection;

/// <summary>Registro en el contenedor de dependencias de los servicios de negocio.</summary>
public static class RegistroCapaNegocio
{
    public static IServiceCollection AgregarCapaNegocio(this IServiceCollection servicios)
    {
        servicios.AgregarCapaDatos();

        // Estado de sesión y seguridad: únicos durante toda la vida de la aplicación.
        servicios.AddSingleton<IServicioHash, ServicioHashBCrypt>();
        servicios.AddSingleton<IContextoSesion, ContextoSesion>();
        servicios.AddSingleton<IServicioConfiguracion, ServicioConfiguracion>();

        // Servicios de dominio. Son sin estado y crean su propia unidad de trabajo
        // por operación, así que registrarlos como únicos evita construcciones repetidas.
        servicios.AddSingleton<IServicioAutenticacion, ServicioAutenticacion>();
        servicios.AddSingleton<IServicioUsuarios, ServicioUsuarios>();
        servicios.AddSingleton<IServicioCategorias, ServicioCategorias>();
        servicios.AddSingleton<IServicioMarcas, ServicioMarcas>();
        servicios.AddSingleton<IServicioUnidadesMedida, ServicioUnidadesMedida>();
        servicios.AddSingleton<IServicioProductos, ServicioProductos>();
        servicios.AddSingleton<IServicioProveedores, ServicioProveedores>();
        servicios.AddSingleton<IServicioClientes, ServicioClientes>();
        servicios.AddSingleton<IServicioKardex, ServicioKardex>();
        servicios.AddSingleton<IServicioInventario, ServicioInventario>();
        servicios.AddSingleton<IServicioCaja, ServicioCaja>();
        servicios.AddSingleton<IServicioCompras, ServicioCompras>();
        servicios.AddSingleton<IServicioVentas, ServicioVentas>();
        servicios.AddSingleton<IServicioCartera, ServicioCartera>();
        servicios.AddSingleton<IServicioDashboard, ServicioDashboard>();
        servicios.AddSingleton<IServicioReportes, ServicioReportes>();
        servicios.AddSingleton<IServicioBackup, ServicioBackup>();
        servicios.AddSingleton<IServicioCodigoBarras, ServicioCodigoBarras>();

        // Cliente HTTP exclusivo para consultar las publicaciones de GitHub. La API
        // exige identificarse con un User-Agent y el tiempo de espera es corto para
        // que un problema de red nunca retrase el arranque de la aplicación.
        servicios.AddHttpClient(nameof(ServicioActualizaciones), cliente =>
        {
            cliente.Timeout = TimeSpan.FromSeconds(30);
            cliente.DefaultRequestHeaders.UserAgent.ParseAdd("PapeleriaApp");
            cliente.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        });

        servicios.AddSingleton<IServicioActualizaciones, ServicioActualizaciones>();
        servicios.AddSingleton<IServicioExportacion, ServicioExportacion>();
        servicios.AddSingleton<IServicioDocumentos, ServicioDocumentos>();

        return servicios;
    }
}
