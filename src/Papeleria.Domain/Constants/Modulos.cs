namespace Papeleria.Domain.Constants;

/// <summary>
/// Claves estables de los módulos del sistema. Se usan como identificador en la
/// tabla de permisos por rol y en la navegación del menú lateral.
/// </summary>
public static class Modulos
{
    public const string Dashboard = "dashboard";
    public const string Productos = "productos";
    public const string Catalogos = "catalogos";
    public const string Proveedores = "proveedores";
    public const string Clientes = "clientes";
    public const string Compras = "compras";
    public const string Ventas = "ventas";
    public const string HistorialVentas = "historialVentas";
    public const string Cartera = "cartera";
    public const string Inventario = "inventario";
    public const string Kardex = "kardex";
    public const string Caja = "caja";
    public const string Reportes = "reportes";
    public const string Configuracion = "configuracion";
    public const string Usuarios = "usuarios";
    public const string Backup = "backup";

    /// <summary>Nombre legible de cada módulo, para el editor de permisos.</summary>
    public static readonly IReadOnlyDictionary<string, string> Nombres = new Dictionary<string, string>
    {
        [Dashboard] = "Dashboard",
        [Productos] = "Productos",
        [Catalogos] = "Categorías, marcas y unidades",
        [Proveedores] = "Proveedores",
        [Clientes] = "Clientes",
        [Compras] = "Compras",
        [Ventas] = "Ventas (POS)",
        [HistorialVentas] = "Historial de ventas",
        [Cartera] = "Cartera (cuentas por cobrar)",
        [Inventario] = "Inventario",
        [Kardex] = "Kardex",
        [Caja] = "Caja",
        [Reportes] = "Reportes",
        [Configuracion] = "Configuración",
        [Usuarios] = "Usuarios y permisos",
        [Backup] = "Copias de seguridad"
    };

    public static IReadOnlyList<string> Todos { get; } = Nombres.Keys.ToList();
}

/// <summary>Claves de la tabla <c>Configuraciones</c> (almacén clave/valor).</summary>
public static class ClavesConfiguracion
{
    public const string EmpresaNombre = "empresa.nombre";
    public const string EmpresaNit = "empresa.nit";
    public const string EmpresaDireccion = "empresa.direccion";
    public const string EmpresaTelefono = "empresa.telefono";
    public const string EmpresaCorreo = "empresa.correo";
    public const string EmpresaCiudad = "empresa.ciudad";
    public const string EmpresaLogo = "empresa.logo";
    public const string EmpresaEslogan = "empresa.eslogan";

    public const string ImpuestoPorDefecto = "impuesto.porDefecto";
    public const string MonedaSimbolo = "moneda.simbolo";
    public const string MonedaCodigo = "moneda.codigo";
    public const string DecimalesMoneda = "moneda.decimales";

    public const string FacturaPrefijo = "factura.prefijo";
    public const string FacturaConsecutivo = "factura.consecutivo";
    public const string FacturaResolucion = "factura.resolucion";
    public const string FacturaPieDePagina = "factura.pie";
    public const string CompraPrefijo = "compra.prefijo";
    public const string CompraConsecutivo = "compra.consecutivo";

    public const string BackupCarpeta = "backup.carpeta";
    public const string BackupAutomatico = "backup.automatico";
    public const string BackupFrecuenciaDias = "backup.frecuenciaDias";
    public const string BackupUltimaFecha = "backup.ultimaFecha";
    public const string BackupRetencion = "backup.retencion";

    public const string ActualizacionesRepositorio = "actualizaciones.repositorio";
    public const string ActualizacionesAutomaticas = "actualizaciones.automaticas";
    public const string ActualizacionesUltimaComprobacion = "actualizaciones.ultimaComprobacion";
    public const string ActualizacionesVersionOmitida = "actualizaciones.versionOmitida";

    public const string TemaOscuro = "ui.temaOscuro";
    public const string ColorPrimario = "ui.colorPrimario";
    public const string UltimoUsuario = "ui.ultimoUsuario";
    public const string RecordarUsuario = "ui.recordarUsuario";
    public const string MenuColapsado = "ui.menuColapsado";
}
