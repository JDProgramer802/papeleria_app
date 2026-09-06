# PapelSoft

**Software de papelería.** Aplicación de escritorio para Windows que administra por completo el negocio:
inventario, punto de venta, compras, caja, kardex, terceros, reportes y respaldos.
Funciona **100 % sin conexión a Internet** sobre una base de datos SQLite local.

- **Framework:** .NET 8 · WPF · MVVM
- **Datos:** SQLite + Entity Framework Core 8 (migraciones automáticas)
- **Interfaz:** Material Design (tema claro y oscuro)
- **Moneda:** peso colombiano (COP), IVA configurable

---

## Puesta en marcha

### Abrir en Visual Studio 2022

1. Abrir `Papeleria.sln`.
2. Proyecto de inicio: **Papeleria.App**.
3. F5.

No hace falta configurar nada más: la base de datos se crea sola en el primer arranque.

### Compilar y ejecutar desde consola

```bash
dotnet build Papeleria.sln -c Release
```

```bash
dotnet run --project src/Papeleria.App/Papeleria.App.csproj
```

### Generar el ejecutable distribuible

Produce un **único `.exe` autónomo** (~87 MB) que no requiere .NET instalado en el
equipo de destino:

```bash
dotnet publish src/Papeleria.App/Papeleria.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish/win-x64
```

El resultado queda en `publish/win-x64/Papeleria.exe`. Se puede copiar a cualquier
equipo con Windows 10/11 de 64 bits y ejecutarlo directamente. Ese archivo suelto es
el que descarga el actualizador; **para entregarle el programa a un cliente use el
instalador**, no el `.exe` crudo.

### Generar el instalador

```bash
powershell -ExecutionPolicy Bypass -File instalador\construir.ps1
```

Publica el ejecutable y lo empaqueta con [Inno Setup 6](https://jrsoftware.org/isinfo.php),
que se instala una sola vez con `winget install --id JRSoftware.InnoSetup --exact`. El
guion toma el número de versión del `.csproj`, así que el instalador y el ejecutable no
pueden contradecirse, y antes de terminar comprueba que los dos binarios se identifican
distinto (ver más abajo por qué eso importa).

El resultado es `publish/instalador/PapelSoft-Instalador.exe` (~82 MB). Lo que hace en
el equipo del cliente:

| | |
|---|---|
| Dónde instala | `%LOCALAPPDATA%\Programs\PapelSoft` |
| Permisos | ninguno: **no pide administrador ni UAC** |
| Accesos directos | escritorio y menú Inicio, siempre, sin preguntar |
| Desinstalación | Configuración → Aplicaciones → Aplicaciones instaladas → PapelSoft |
| Pasos del asistente | bienvenida → progreso → fin |

Se instala en la carpeta del usuario y no en `Archivos de programa` por dos razones que
pesan más que la costumbre: el dueño de una papelería rara vez es administrador de su
equipo, y el programa se actualiza solo reemplazando su propio ejecutable —cosa que en
`Archivos de programa` Windows no permite—.

**Instálelo siempre desde la sesión de Windows que el cliente usa a diario**, nunca con
«Ejecutar como administrador» ni desde otra cuenta: los datos viven en el perfil de cada
usuario, así que instalarlo desde otra cuenta hace que el programa abra una base vacía y
el cliente crea que perdió su información. El asistente lo advierte en su primera
pantalla.

### Desinstalar

Configuración → Aplicaciones → Aplicaciones instaladas → PapelSoft → Desinstalar.

**Los datos del negocio no se borran.** La base de datos, las imágenes y los respaldos
viven en `%LOCALAPPDATA%\PapeleriaApp`, fuera de la carpeta del programa, y el
desinstalador no los toca: si se vuelve a instalar en la misma cuenta de Windows, todo
sigue como estaba. Para eliminarlos de verdad hay que borrar esa carpeta a mano.

### Primer acceso

| Usuario | Contraseña  |
|---------|-------------|
| `admin` | `Admin123*` |

La pantalla de login muestra estas credenciales mientras no se hayan cambiado.
**Cámbielas desde _Usuarios → Mi contraseña_ en cuanto entre por primera vez.**

---

## Dónde viven los datos

Todo se guarda bajo `%LOCALAPPDATA%\PapeleriaApp`, fuera de la carpeta del programa,
para que la aplicación funcione sin permisos de administrador y actualizar el `.exe`
nunca borre información:

```
%LOCALAPPDATA%\PapeleriaApp\
├── Data\papeleria.db      Base de datos SQLite
├── Backups\               Copias de seguridad
├── Images\                Imágenes de productos y logo
├── Exportaciones\         Reportes exportados
├── Logs\                  Registro de errores (30 días)
└── Temp\                  Facturas y etiquetas generadas
```

---

## Arquitectura

Cuatro proyectos con dependencias en una sola dirección:

```
Papeleria.Domain  ←  Papeleria.Data  ←  Papeleria.Business  ←  Papeleria.App
```

| Proyecto | Responsabilidad |
|---|---|
| **Papeleria.Domain** | Entidades, enumerados, excepciones de negocio y constantes. No depende de nada. |
| **Papeleria.Data** | `AppDbContext`, configuraciones EF, migraciones, unidad de trabajo, repositorios y siembra de datos maestros. |
| **Papeleria.Business** | Servicios de negocio (ventas, compras, caja, kardex, reportes, respaldos…), DTOs y seguridad. |
| **Papeleria.App** | WPF: vistas, modelos de vista, controles, convertidores y arranque con inyección de dependencias. |

La navegación es **ViewModel-first**: se navega por clave de módulo, el contenedor
resuelve el modelo de vista y un `DataTemplate` (`Resources/PlantillasDatos.xaml`)
elige la vista. Los modelos de vista no conocen ningún tipo de WPF.

---

## Módulos

| Módulo | Qué hace |
|---|---|
| **Dashboard** | Nueve indicadores, gráfico comparativo de 12 meses, productos más vendidos, movimientos recientes y alertas accionables. |
| **Punto de venta** | Facturación con lector de código de barras, carrito editable, descuentos, diálogo de cobro con cambio e impresión del recibo. El cliente se busca escribiendo y se puede dar de alta uno nuevo sin salir de la venta. |
| **Productos** | CRUD completo, búsqueda paginada, duplicado, generación de código de barras EAN-13 e impresión de etiquetas. Distingue mercancía de **servicios** (fotocopias, impresiones, anillado), que se cobran sin descontar existencias, y permite declarar cuántas unidades trae la presentación de compra para comprar por caja y vender por unidad. El botón **Comprar** abre el formulario de compra con el artículo ya cargado. |
| **Inventario** | Existencias con semáforo, entradas, salidas, ajustes por conteo y transferencias de ubicación. |
| **Compras** | Historial y registro de compras: actualiza existencias, recalcula el costo promedio y escribe en el kardex. |
| **Cartera** | Cuentas por cobrar de las ventas a crédito: quién debe y desde cuándo, cupo por cliente, estado de cuenta con las facturas fiadas y lo pendiente de cada una, registro y anulación de abonos, y reparto de la deuda por antigüedad. |
| **Caja** | Apertura con base, ingresos, egresos, arqueo comparando esperado contra contado, cierre e historial de turnos. |
| **Historial de ventas** | Registro de todas las facturas emitidas: rangos rápidos (hoy, ayer, semana, mes) o fechas a medida, búsqueda por número, cliente o método de pago, resumen del periodo con importe, ticket promedio y utilidad, detalle con los productos de cada factura, impresión de una factura suelta o del listado completo del periodo, anulación y exportación. |
| **Devoluciones** | Devolución parcial de una factura desde el historial: se eligen los renglones y las cantidades, la mercancía vuelve al inventario, queda registrada en el kardex y el dinero sale del turno de caja. La factura no se anula ni se rehace. |
| **Kardex** | Consulta filtrable del histórico de movimientos, con exportación. |
| **Clientes / Proveedores** | Directorios con ficha e historial de documentos. |
| **Catálogos** | Categorías, marcas y unidades de medida. |
| **Reportes** | Doce informes con vista previa y exportación a Excel, PDF y CSV. |
| **Configuración** | Datos de empresa, logo, impuestos, moneda, numeración de documentos, tema y copias de seguridad. |
| **Usuarios** | Usuarios, roles y matriz de permisos editable por módulo. |
| **Manual de uso** | Guía de uso dentro del propio programa, con buscador sobre el texto completo, y un tutorial guiado que revisa la base de datos y señala qué pasos de la puesta en marcha siguen pendientes, con acceso directo al módulo de cada uno. |

---

## Reglas de negocio garantizadas

- **El kardex es inmutable.** Además de bloquearlo en el código, la migración instala
  disparadores en SQLite que rechazan `UPDATE` y `DELETE` sobre `MovimientosKardex`,
  de modo que ninguna herramienta externa pueda alterar el histórico.
- **No se vende sin existencias.** El carrito completo se valida antes de tocar nada,
  para que el inventario nunca quede parcialmente descontado.
- **No se factura con la caja cerrada.** El dinero siempre tiene un turno donde registrarse.
- **Nada se pierde.** Productos, clientes, proveedores y usuarios con movimientos se
  desactivan en lugar de borrarse. Las ventas y compras se anulan, nunca se eliminan.
- **Todo es transaccional.** Una venta toca factura, detalle, existencias, kardex, caja
  y consecutivo dentro de una sola transacción: o se guarda todo, o no se guarda nada.
- **Solo el administrador anula facturas**, porque mueve inventario y dinero.

### Cómo se calculan los importes

Sobre cada línea se aplica primero el descuento y sobre la base resultante se liquida
el IVA, que es el orden de la facturación colombiana:

```
subtotal      = cantidad × valor unitario
descuento     = subtotal × % descuento
base gravable = subtotal − descuento
IVA           = base gravable × % IVA
total         = base gravable + IVA
```

El costo de compra se capitaliza **sin IVA** y se mezcla con el inventario existente
mediante promedio ponderado. La utilidad de una venta es `base gravable − costo de la
mercancía vendida`, con el costo congelado en el momento de facturar.

---

## Decisiones técnicas que conviene conocer

**Los gráficos son controles WPF propios** (`Controls/GraficoBarras.cs`). Se descartó
LiveCharts2 porque arrastra SkiaSharp y OpenTK compilados para .NET Framework 4.6.1:
binarios nativos y avisos `NU1701` en un ejecutable que debe ser autónomo y funcionar
sin conexión. El control propio dibuja ejes, rejilla, etiquetas, tooltips y animación
de entrada sin ninguna dependencia nativa.

**Los códigos de barras no usan System.Drawing.** ZXing genera la matriz y
`Common/CodificadorPng.cs` la codifica a PNG con un codificador propio de ~120 líneas,
para que el mismo byte array sirva igual en pantalla (WPF) y en los PDF (QuestPDF).

**Los decimales se guardan como REAL.** SQLite no tiene tipo decimal nativo; EF los
guardaría como TEXT y dejaría de traducir `SUM` y `ORDER BY` a SQL. `AppDbContext`
mapea globalmente los `decimal` a `double` en el proveedor y la capa de negocio
redondea con `Dinero.Redondear` antes de persistir. Donde SQLite sigue sin poder
ordenar (agregaciones del ranking de ventas) la ordenación se resuelve en `double`
y la conversión a `decimal` se hace ya en memoria.

**Material Design 2.** `App.xaml` carga `MaterialDesign2.Defaults.xaml`, que es el
juego de estilos que define los nombres clásicos (`MaterialDesignRaisedButton`,
`MaterialDesignOutlinedTextBox`…) sobre los que se construye `Resources/Estilos.xaml`.

---

## Migraciones

La aplicación aplica las migraciones pendientes en cada arranque. Para añadir una nueva
tras cambiar el modelo:

```bash
dotnet dotnet-ef migrations add NombreDeLaMigracion --project src/Papeleria.Data --startup-project src/Papeleria.Data
```

La herramienta `dotnet-ef` ya está fijada en `.config/dotnet-tools.json`; si es la
primera vez en el equipo, restáurela con:

```bash
dotnet tool restore
```

---

## Publicar una actualización

La aplicación se actualiza sola desde las publicaciones («releases») de un repositorio
de GitHub. El cliente no tiene que descargar nada a mano.

### Configuración inicial (una sola vez)

En la aplicación: **Configuración → Actualizaciones**, escribir el repositorio con el
formato `usuario/repositorio` (también se acepta pegar la URL completa de GitHub) y
guardar. El repositorio debe ser **público**, porque la comprobación se hace sin
credenciales.

### Cada vez que quiera publicar una versión nueva

1. **Subir el número de versión** en `src/Papeleria.App/Papeleria.App.csproj`:

   ```xml
   <Version>1.0.1</Version>
   <AssemblyVersion>1.0.1.0</AssemblyVersion>
   <FileVersion>1.0.1.0</FileVersion>
   ```

   Este paso no es opcional: la app compara la versión del ensamblado con la etiqueta
   de la release, y si no sube el número no detectará nada.

2. **Generar el ejecutable y el instalador** con `instalador\construir.ps1`.

3. **Crear la release en GitHub** con la etiqueta `v1.0.1` (vale `1.0.1`, `v1.0.1` o
   `version-1.0.1`) y adjuntar los dos archivos:

   | Archivo | Para quién |
   |---|---|
   | `Papeleria.exe` | para el programa: es el que descarga el actualizador |
   | `PapelSoft-Instalador.exe` | para las personas: es el que se instala en el equipo |

   **El nombre `Papeleria.exe` no se puede cambiar.** El actualizador busca el adjunto
   que se llama exactamente igual que el ejecutable en marcha; si no lo encuentra,
   prefiere no ofrecer la actualización antes que aplicar el archivo equivocado.

   El instalador tampoco cambia de nombre, y a propósito no lleva la versión dentro:
   así el enlace
   `https://github.com/<usuario>/<repo>/releases/latest/download/PapelSoft-Instalador.exe`
   apunta siempre a la última versión y se puede mandar por WhatsApp una sola vez.

   Escriba las notas de la release **en frases planas, sin markdown**: el programa las
   muestra tal cual dentro del aviso de actualización, y los `##` y los `**` se ven
   como basura.

   > **Excepción de la 2.3.0.** El filtro por nombre exacto vive en el ejecutable del
   > cliente, no en GitHub, así que las copias anteriores a la 2.3.0 siguen tomando «el
   > primer `.exe`» que encuentren. Por eso la release 2.3.0 adjunta el instalador
   > comprimido, `PapelSoft-Instalador.zip`, y deja `Papeleria.exe` como único `.exe`
   > de la lista. De la 2.4.0 en adelante, con el parque ya actualizado, el instalador
   > vuelve a ir suelto como `.exe`.

Al abrir el programa, el cliente verá el aviso de versión nueva con sus notas, podrá
instalarla con un clic y reiniciar. También puede buscarlas a mano desde
**Configuración → Actualizaciones → Buscar ahora**.

### Cómo se comporta

- **Comprueba una vez al día como máximo**, en segundo plano y después de abrir la
  pantalla principal: si no hay Internet o GitHub no responde, no se muestra nada y el
  programa funciona con normalidad.
- **Verifica la descarga** contra la huella SHA-256 que publica GitHub; un archivo
  incompleto o alterado se descarta sin instalarse.
- **Comprueba que lo descargado es el programa** y no otro ejecutable de la misma
  release. El tamaño y la huella no bastan para esto: los dos salen del mismo adjunto,
  así que el archivo equivocado los pasaría con nota perfecta. Lo que se mira es qué
  dice ser el binario: el programa se identifica como «PapelSoft» y el instalador como
  «PapelSoft (Instalador)».
- **Conserva el ejecutable anterior** con la extensión `.anterior` hasta el siguiente
  arranque, por si hubiera que volver atrás.
- **No toca los datos.** La base de datos, los respaldos y la configuración viven en
  `%LOCALAPPDATA%`, así que una actualización nunca puede perderlos.
- El usuario puede pulsar **«Omitir esta versión»** para que no se le vuelva a avisar
  de esa versión concreta.

### Limitaciones

- Solo funciona sobre el **ejecutable publicado**. Si se ejecuta la compilación de
  desarrollo, la pantalla lo indica y desactiva la instalación automática.
- Si el programa está en `C:\Program Files`, Windows no deja sustituirlo sin permisos
  de administrador y la actualización automática queda desactivada. Por eso el
  instalador lo pone en `%LOCALAPPDATA%\Programs\PapelSoft`, donde el propio usuario
  sí puede escribir. Si alguna vez aparece ese aviso, la salida es volver a ejecutar el
  instalador con la cuenta de siempre; nunca elevar como administrador, porque con otra
  cuenta el programa abriría una base de datos vacía.
- **La versión que muestra «Aplicaciones instaladas» se queda congelada** en la que puso
  el instalador: las actualizaciones automáticas reemplazan el ejecutable pero no tocan
  el registro de Windows. Para saber qué versión tiene un cliente, pregunte por la que
  muestra el propio programa, no por la del panel de Windows.
- El instalador **no está firmado digitalmente**. La primera vez, el navegador avisa de
  una descarga poco frecuente y SmartScreen muestra «Windows protegió su PC»: hay que
  pulsar *Más información → Ejecutar de todas formas*. Las actualizaciones posteriores
  no pasan por ahí, así que el único momento incómodo es la instalación inicial.

---

## Copias de seguridad

- Se crean con la API de respaldo en línea de SQLite, así que **no hace falta cerrar
  la aplicación** para generarlas.
- Automáticas al cerrar el programa, con frecuencia y retención configurables.
- Manuales desde el icono de la barra superior o desde _Configuración → Copias de seguridad_.
- Al restaurar se guarda primero una copia del estado vigente, se valida que el archivo
  sea realmente una base de datos del sistema y la aplicación se cierra para que el
  siguiente arranque trabaje con los datos restaurados.
