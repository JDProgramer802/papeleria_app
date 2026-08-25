using MaterialDesignThemes.Wpf;

namespace Papeleria.App.Ayuda;

/// <summary>Tipo de bloque de texto dentro de un apartado del manual.</summary>
public enum TipoBloque
{
    /// <summary>Texto corrido.</summary>
    Parrafo = 0,

    /// <summary>Instrucciones numeradas que se siguen en orden.</summary>
    Pasos = 1,

    /// <summary>Puntos sueltos sin orden concreto.</summary>
    Lista = 2,

    /// <summary>Advertencia que conviene leer antes de actuar.</summary>
    Aviso = 3,

    /// <summary>Recomendación práctica del oficio.</summary>
    Consejo = 4
}

/// <summary>Bloque de contenido dentro de un apartado.</summary>
public class BloqueManual
{
    public required TipoBloque Tipo { get; init; }

    /// <summary>Encabezado del bloque; opcional en los párrafos.</summary>
    public string? Titulo { get; init; }

    public string Texto { get; init; } = string.Empty;

    public IReadOnlyList<string> Puntos { get; init; } = Array.Empty<string>();

    public bool EsParrafo => Tipo == TipoBloque.Parrafo;
    public bool EsPasos => Tipo == TipoBloque.Pasos;
    public bool EsLista => Tipo == TipoBloque.Lista;
    public bool EsAviso => Tipo == TipoBloque.Aviso;
    public bool EsConsejo => Tipo == TipoBloque.Consejo;

    public bool TieneTitulo => !string.IsNullOrWhiteSpace(Titulo);
}

/// <summary>Apartado del manual, tal como aparece en el índice.</summary>
public class SeccionManual
{
    public required string Titulo { get; init; }

    public required string Resumen { get; init; }

    public required PackIconKind Icono { get; init; }

    public IReadOnlyList<BloqueManual> Bloques { get; init; } = Array.Empty<BloqueManual>();

    /// <summary>Texto completo del apartado, para que el buscador encuentre por dentro.</summary>
    public string TextoBuscable =>
        string.Join(' ', new[] { Titulo, Resumen }
            .Concat(Bloques.Select(b => b.Titulo ?? string.Empty))
            .Concat(Bloques.Select(b => b.Texto))
            .Concat(Bloques.SelectMany(b => b.Puntos)));
}

/// <summary>
/// Manual de uso del sistema. Vive dentro del programa porque la papelería trabaja
/// sin conexión: un manual en la nube no serviría de nada un día sin internet.
/// </summary>
public static class ContenidoManual
{
    private static BloqueManual Parrafo(string texto) =>
        new() { Tipo = TipoBloque.Parrafo, Texto = texto };

    private static BloqueManual Pasos(string titulo, params string[] puntos) =>
        new() { Tipo = TipoBloque.Pasos, Titulo = titulo, Puntos = puntos };

    private static BloqueManual Lista(string titulo, params string[] puntos) =>
        new() { Tipo = TipoBloque.Lista, Titulo = titulo, Puntos = puntos };

    private static BloqueManual Aviso(string texto) =>
        new() { Tipo = TipoBloque.Aviso, Texto = texto };

    private static BloqueManual Consejo(string texto) =>
        new() { Tipo = TipoBloque.Consejo, Texto = texto };

    public static IReadOnlyList<SeccionManual> Secciones { get; } = new List<SeccionManual>
    {
        new()
        {
            Titulo = "Primeros pasos",
            Resumen = "Qué hacer el primer día, en orden",
            Icono = PackIconKind.FlagOutline,
            Bloques = new[]
            {
                Parrafo(
                    "El programa guarda todo en su propio computador y funciona sin internet. " +
                    "No hay que instalar nada más ni pagar una mensualidad."),
                Pasos("Montar la papelería desde cero",
                    "Ponga los datos de su negocio en Configuración: el nombre y el NIT salen impresos en cada factura.",
                    "Cambie la contraseña del usuario «admin». La de fábrica la conoce cualquiera.",
                    "Cree sus categorías en Catálogos: cuadernos, escritura, oficina, arte…",
                    "Registre sus productos con su código, su precio y las existencias que tenga hoy.",
                    "Registre sus servicios: fotocopias, impresiones, anillado.",
                    "Anote sus proveedores y registre la primera compra para que el costo quede real.",
                    "Abra la caja con el dinero con el que empieza el día.",
                    "Haga una venta de prueba y compruebe que el recibo sale bien."),
                Consejo(
                    "Use el Tutorial guiado: mira su base de datos y le dice cuáles de estos pasos " +
                    "ya están resueltos y cuáles siguen pendientes.")
            }
        },
        new()
        {
            Titulo = "El panel de inicio",
            Resumen = "Qué mirar cada mañana y qué significa cada cifra",
            Icono = PackIconKind.ViewDashboardOutline,
            Bloques = new[]
            {
                Parrafo(
                    "Es la primera pantalla al entrar. Está ordenada de arriba abajo por urgencia: " +
                    "lo de la fila de arriba es lo que puede costarle plata hoy."),
                Lista("La fila de arriba",
                    "Efectivo en caja: lo que debería haber en el cajón en este momento, contando " +
                    "las ventas de contado, los ingresos y los egresos del turno. Si no cuadra al " +
                    "cerrar, la diferencia sale ahí.",
                    "Nos deben (fiado): la suma de lo que le deben todos los clientes. La etiqueta " +
                    "roja avisa de la parte con más de 60 días, que es la que se vuelve incobrable.",
                    "Plata quieta en la estantería: lo que le costó la mercancía que lleva noventa " +
                    "días sin venderse. Es dinero suyo detenido en una repisa."),
                Parrafo(
                    "Las dos primeras solo aparecen si su usuario tiene esos módulos. A un vendedor " +
                    "no le sale el dinero del cajón ni la deuda de los clientes."),
                Lista("Cómo se comparan las ventas",
                    "Ventas del mes se compara contra el mismo mes del año pasado, no contra el mes " +
                    "anterior. Diciembre siempre le gana a noviembre; compararlos no dice nada.",
                    "Mientras no haya un año de historia, el panel compara contra el mes anterior y " +
                    "lo dice en la propia tarjeta.",
                    "Las ventas de hoy se comparan contra el mismo día de la semana pasada. Un martes " +
                    "no se parece a un sábado."),
                Lista("Las alertas",
                    "Productos que se venden por debajo del costo: subió el proveedor y el precio se " +
                    "quedó viejo. Cada venta de esos productos pierde plata.",
                    "Deuda con más de 60 días: conviene llamar hoy.",
                    "Caja abierta hace más de doce horas: casi siempre es un cierre que se olvidó.",
                    "Productos agotados o bajo el mínimo: hay que reponer."),
                Consejo(
                    "Cada tarjeta y cada alerta se puede pulsar: lo lleva directo al módulo donde " +
                    "se arregla lo que le está avisando.")
            }
        },
        new()
        {
            Titulo = "Punto de venta",
            Resumen = "Cobrar, imprimir y fiar",
            Icono = PackIconKind.CashRegister,
            Bloques = new[]
            {
                Parrafo(
                    "Es la pantalla del mostrador. Todo está pensado para cobrar rápido: el cursor " +
                    "queda siempre en el campo de búsqueda para que el lector de código de barras funcione solo."),
                Pasos("Hacer una venta",
                    "Pase el producto por el lector, o escriba parte del nombre y elíjalo de la lista.",
                    "Ajuste la cantidad en el carrito si vende más de uno.",
                    "Elija el cliente. Si es alguien de paso, deje «Consumidor final».",
                    "Pulse COBRAR, escoja el medio de pago y confirme."),
                Lista("Medios de pago",
                    "Efectivo: escriba lo que le entregan y el sistema calcula el cambio.",
                    "Tarjeta y transferencia: no entran al cajón, pero sí quedan en la venta.",
                    "Crédito: la venta queda a deber y aparece en Cartera. Solo si el cliente tiene cupo.",
                    "Mixto: parte en efectivo y el resto por otro medio."),
                Aviso(
                    "Sin caja abierta no se puede facturar. Es a propósito: el dinero necesita un turno " +
                    "al que pertenecer, o el arqueo del final del día no cuadra."),
                Consejo(
                    "¿Le llegó un cliente nuevo en plena venta? El botón junto al selector de cliente " +
                    "lo da de alta sin salir de la factura.")
            }
        },
        new()
        {
            Titulo = "Productos y servicios",
            Resumen = "Mercancía, fotocopias y presentaciones",
            Icono = PackIconKind.PackageVariantClosed,
            Bloques = new[]
            {
                Parrafo(
                    "El catálogo distingue dos cosas que se cobran igual pero se comportan distinto."),
                Lista("Producto o servicio",
                    "Producto: es mercancía. Descuenta existencias al venderse y queda en el kardex.",
                    "Servicio: fotocopias, impresiones, anillado. Se cobra sin descontar nada, nunca se agota y no se compra a proveedores."),
                Parrafo(
                    "Muchos artículos se compran por caja y se venden sueltos. En la ficha del producto " +
                    "indique cuántas unidades trae la presentación: si la caja de lápices trae doce, escriba 12."),
                Pasos("Comprar por caja y vender por unidad",
                    "En el producto, ponga 12 en «Unidades por presentación».",
                    "Al registrar la compra, marque la casilla «Por caja» en esa línea.",
                    "Escriba 2 cajas a $12.000 cada una.",
                    "El sistema mete 24 unidades al inventario, a $1.000 cada una."),
                Consejo(
                    "El botón «Comprar» del catálogo abre el formulario de compra con ese artículo " +
                    "ya puesto, sin tener que buscarlo otra vez.")
            }
        },
        new()
        {
            Titulo = "Inventario y kardex",
            Resumen = "Existencias, ajustes y el histórico que no se toca",
            Icono = PackIconKind.Warehouse,
            Bloques = new[]
            {
                Parrafo(
                    "El inventario muestra lo que hay hoy. El kardex muestra cómo se llegó hasta ahí: " +
                    "cada entrada y cada salida, con su fecha, su motivo y quién la hizo."),
                Lista("Movimientos que puede registrar a mano",
                    "Entrada: mercancía que llega sin una compra de por medio.",
                    "Salida: material que se usa en el negocio o que se dañó.",
                    "Ajuste: después de contar físicamente, deja la existencia en lo que hay de verdad.",
                    "Transferencia: cambia la ubicación sin alterar la cantidad total."),
                Aviso(
                    "El kardex no se puede modificar ni borrar, ni siquiera por el administrador. " +
                    "La propia base de datos lo impide. Si algo quedó mal, se corrige con un ajuste " +
                    "que deja constancia, nunca borrando el error.")
            }
        },
        new()
        {
            Titulo = "Caja y arqueo",
            Resumen = "Abrir el turno, contar y cerrar",
            Icono = PackIconKind.CashMultiple,
            Bloques = new[]
            {
                Parrafo(
                    "La caja es un turno: se abre con una base, se mueve durante el día y se cierra contando."),
                Pasos("El día completo",
                    "Al empezar, abra la caja con el dinero de la base.",
                    "Venda con normalidad; el efectivo se va sumando solo.",
                    "Registre ingresos o egresos si saca o mete dinero por otra razón.",
                    "Al cerrar, cuente lo que hay físicamente y escríbalo.",
                    "El sistema compara con lo esperado y muestra el sobrante o el faltante."),
                Lista("Qué entra al cajón y qué no",
                    "Efectivo de las ventas: sí.",
                    "Tarjeta y transferencia: no, aunque sí cuentan como venta.",
                    "Ventas a crédito: no, quedan en Cartera.",
                    "Abonos en efectivo de clientes: sí.",
                    "Devoluciones de ventas cobradas en efectivo: salen del cajón.")
            }
        },
        new()
        {
            Titulo = "Devoluciones",
            Resumen = "Devolver parte de una factura sin anularla",
            Icono = PackIconKind.KeyboardReturn,
            Bloques = new[]
            {
                Parrafo(
                    "Si el cliente devuelve dos cuadernos de una factura de quince renglones, no hay " +
                    "que anular la factura entera ni rehacerla: eso rompería el consecutivo."),
                Pasos("Recibir una devolución",
                    "Vaya a Historial de ventas y busque la factura.",
                    "Pulse «Devolver» en el panel del detalle.",
                    "Ajuste cuántas unidades vuelven de cada renglón con los botones + y −.",
                    "Escriba el motivo y confirme."),
                Lista("Qué pasa al confirmar",
                    "La mercancía vuelve al inventario y queda en el kardex.",
                    "El dinero sale del turno de caja abierto.",
                    "La factura sigue vigente, con su consecutivo intacto.",
                    "Queda registrada con su propio número y su motivo."),
                Aviso(
                    "No se puede devolver más de lo vendido ni dos veces lo mismo: la segunda vez que " +
                    "abra la factura solo verá lo que aún queda por devolver.")
            }
        },
        new()
        {
            Titulo = "Cartera: lo que le deben",
            Resumen = "Fiar con cupo y recibir abonos",
            Icono = PackIconKind.AccountCashOutline,
            Bloques = new[]
            {
                Parrafo(
                    "Fiar sin control es la forma más común de perder plata en una papelería. " +
                    "El sistema solo deja fiar a quien tiene cupo asignado, y nunca al consumidor final."),
                Pasos("Fiarle a un cliente",
                    "Edite el cliente y póngale un cupo de crédito.",
                    "En el punto de venta elíjalo y cobre con el medio «Crédito».",
                    "El diálogo le dice cuánto cupo le quedará."),
                Pasos("Cobrar la deuda",
                    "Vaya a Cartera: verá quién debe, cuánto y desde hace cuántos días.",
                    "Seleccione el cliente y pulse REGISTRAR ABONO.",
                    "El abono en efectivo entra al turno de caja abierto."),
                Consejo(
                    "Las tarjetas de arriba reparten la deuda por antigüedad. Empiece a llamar por " +
                    "la de «más de 60 días»: es la que se vuelve incobrable.")
            }
        },
        new()
        {
            Titulo = "Compras a proveedores",
            Resumen = "Recibir mercancía y actualizar el costo",
            Icono = PackIconKind.TruckDeliveryOutline,
            Bloques = new[]
            {
                Parrafo(
                    "Registrar la compra hace tres cosas a la vez: sube las existencias, recalcula el " +
                    "costo promedio del producto y deja el movimiento en el kardex."),
                Aviso(
                    "Si no registra las compras, el costo queda en cero y todas las utilidades que " +
                    "vea en los reportes serán falsas."),
                Consejo(
                    "El costo se recalcula como promedio ponderado: si tenía 10 unidades a $1.000 y " +
                    "compra 10 a $1.200, el costo pasa a $1.100. Es lo correcto para saber cuánto gana de verdad.")
            }
        },
        new()
        {
            Titulo = "Reportes",
            Resumen = "Consultar, exportar e imprimir",
            Icono = PackIconKind.ChartBoxOutline,
            Bloques = new[]
            {
                Parrafo(
                    "Doce informes con vista previa. Elija uno en la lista, ajuste el periodo y pulse GENERAR."),
                Lista("Los que más se usan",
                    "Ventas: todas las facturas del periodo con su medio de pago.",
                    "Ganancias por producto: qué le deja dinero de verdad y qué no.",
                    "Inventario valorizado: cuánta plata tiene quieta en la estantería.",
                    "Productos con poco stock: qué hay que pedir.",
                    "Cartera por cobrar: quién le debe, ordenado por antigüedad.",
                    "Caja: los turnos con sus arqueos y diferencias."),
                Parrafo(
                    "Todo informe se exporta a Excel, PDF o CSV, o se manda directo a la impresora."),
                Aviso(
                    "Si un informe supera las cinco mil filas se recorta y avisa, tanto en pantalla " +
                    "como dentro del archivo exportado. Acote el periodo para verlo completo.")
            }
        },
        new()
        {
            Titulo = "Respaldos y seguridad",
            Resumen = "No perder el trabajo de años",
            Icono = PackIconKind.DatabaseArrowDown,
            Bloques = new[]
            {
                Parrafo(
                    "Todos sus datos viven en un solo archivo. Respaldarlo es copiar ese archivo a otro sitio."),
                Pasos("Dejarlo funcionando",
                    "Vaya a Configuración → Copias de seguridad.",
                    "Elija una carpeta en una memoria USB o en otro disco.",
                    "Deje activada la copia automática al cerrar el programa."),
                Aviso(
                    "Si guarda los respaldos en el mismo disco donde está el programa, el día que ese " +
                    "disco falle perderá los datos y las copias a la vez."),
                Lista("Sobre los usuarios",
                    "Cada quien con su usuario: así el kardex y las ventas dicen quién hizo qué.",
                    "El cajero no ve costos ni ganancias; el de bodega no toca la caja.",
                    "Los permisos por rol se ajustan en Usuarios, módulo por módulo.")
            }
        },
        new()
        {
            Titulo = "Preguntas frecuentes",
            Resumen = "Lo que suele trabar a quien empieza",
            Icono = PackIconKind.HelpCircleOutline,
            Bloques = new[]
            {
                Lista("«No me deja facturar»",
                    "Casi siempre es que la caja está cerrada. Ábrala desde el módulo Caja.",
                    "Si es un producto concreto, revise que esté activo y con existencias."),
                Lista("«No me deja fiar»",
                    "Al consumidor final no se le fía nunca: registre al cliente.",
                    "El cliente necesita un cupo de crédito en su ficha.",
                    "Si ya debe más de su cupo, primero tiene que abonar."),
                Lista("«El inventario no cuadra»",
                    "Cuente físicamente y use un ajuste de inventario; queda registrado con su motivo.",
                    "Revise el kardex del producto: ahí está cada movimiento con su responsable."),
                Lista("«La caja me sale con faltante»",
                    "Compruebe que registró los egresos que hizo durante el día.",
                    "Recuerde que las devoluciones en efectivo salen del cajón."),
                Parrafo(
                    "Si el programa muestra un error, el detalle queda guardado. En Configuración → " +
                    "Empresa encontrará el enlace «Ver registro de errores».")
            }
        }
    };
}
