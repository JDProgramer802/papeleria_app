; ══════════════════════════════════════════════════════════════════════════════
;  Instalador de PapelSoft
;
;  Se compila con Inno Setup 6. El número de versión no se escribe aquí: lo pasa
;  construir.ps1 leyéndolo del .csproj, para que el instalador, el ejecutable y la
;  etiqueta de GitHub no puedan contradecirse.
;
;      ISCC.exe PapelSoft.iss /DVersion=2.3.0 /DOrigen=..\publish\win-x64\Papeleria.exe
;
;  ── POR QUÉ SE INSTALA EN LA CARPETA DEL USUARIO Y NO EN «ARCHIVOS DE PROGRAMA»
;
;  Dos razones, y las dos pesan más que la costumbre:
;
;    1. El dueño de una papelería rara vez es administrador de su propio equipo. Una
;       instalación en Archivos de programa le pide una contraseña que no tiene.
;    2. El programa se actualiza solo: descarga la versión nueva y reemplaza su propio
;       ejecutable. En Archivos de programa no puede escribir, y ServicioActualizaciones
;       lo detecta y se rinde (ComprobarViabilidad → CarpetaSoloLectura).
;
;  Con PrivilegesRequired=lowest, {autopf} se resuelve a %LOCALAPPDATA%\Programs.
;
;  ── LOS DATOS NO VIVEN AQUÍ
;
;  La base de datos, los respaldos, las imágenes y los logs viven en
;  %LOCALAPPDATA%\PapeleriaApp, fuera de la carpeta de instalación. Por eso desinstalar
;  no puede borrar la información del negocio.
;
;  Ojo al parecido de los nombres: el producto es «PapelSoft» y la carpeta de datos es
;  «PapeleriaApp». Nada en este archivo debe nombrar esa segunda ruta para escribir ni
;  para borrar. En concreto, nunca escribir:
;    - DefaultDirName apuntando dentro de {localappdata}\PapeleriaApp
;    - [UninstallDelete] Type: filesandordirs sobre esa ruta
;    - [UninstallDelete] Type: filesandordirs sobre {app}, porque con /DIR= alguien
;      pudo apuntar {app} a Documentos o al Escritorio
;  Inno borra al desinstalar todo lo que instaló: si algún día se copia algo dentro de
;  la carpeta de datos, se lo lleva. Que las carpetas de datos las siga creando la
;  aplicación (RutasAplicacion.AsegurarCarpetas).
; ══════════════════════════════════════════════════════════════════════════════

#define NombreApp        "PapelSoft"
#define Editor           "PapelSoft"
#define Ejecutable       "Papeleria.exe"
#define Repositorio      "https://github.com/JDProgramer802/papeleria_app"

#ifndef Version
  #define Version "0.0.0"
#endif

#ifndef Origen
  #define Origen "..\publish\win-x64\Papeleria.exe"
#endif

[Setup]
; El AppId identifica al producto entre versiones y no debe cambiar nunca. Si cambia,
; Windows deja de ver la instalación anterior y el cliente termina con dos PapelSoft
; en la lista de aplicaciones, dos carpetas y dos accesos directos.
AppId={{8CF36342-1532-4D5F-AFBA-F227F07AA8FF}
AppName={#NombreApp}
AppVersion={#Version}
AppVerName={#NombreApp} {#Version}
VersionInfoVersion={#Version}
; El instalador se identifica como distinto de la aplicación a propósito: el
; actualizador comprueba el ProductName de lo que descarga antes de aplicarlo.
VersionInfoProductName={#NombreApp} (Instalador)
AppPublisher={#Editor}
AppPublisherURL={#Repositorio}
AppSupportURL={#Repositorio}/issues
AppUpdatesURL={#Repositorio}/releases

; Instalación por usuario: ni UAC ni contraseña de administrador.
PrivilegesRequired=lowest
; «commandline» y no «dialog»: con dialog el asistente pregunta «¿para todos los
; usuarios o solo para mí?», y si el cliente elige «todos» —o si alguien lo ejecuta
; como administrador con OTRA cuenta— la aplicación, el icono y los datos acaban en el
; perfil de esa otra cuenta. El dueño abre el programa y ve su negocio en blanco.
PrivilegesRequiredOverridesAllowed=commandline
DefaultDirName={autopf}\{#NombreApp}
DefaultGroupName={#NombreApp}
UsePreviousAppDir=yes

; El programa es de 64 bits y corre sobre .NET 8, que pide Windows 10 en adelante.
; Sin estas dos líneas la instalación termina bien en un equipo viejo y la aplicación
; falla al abrir, que es la peor forma de enterarse.
ArchitecturesAllowed=x64compatible
MinVersion=10.0.14393

; Si el programa está abierto se le pide al usuario que lo cierre él mismo, por su
; propia ventana. CloseApplications=no es deliberado: dejar que el Restart Manager lo
; cierre por su cuenta puede cortar una venta a medias y, peor, interrumpir el respaldo
; automático de cierre dejando una copia truncada en la carpeta de respaldos.
; El nombre es exactamente el mutex que crea App.xaml.cs (NombreInstanciaUnica).
AppMutex=Local\PapeleriaApp.InstanciaUnica
CloseApplications=no
RestartApplications=no

; Bienvenida → progreso → Fin. Sin páginas que obliguen a decidir cosas que el dueño de
; una papelería no tiene por qué decidir. Quien sepa lo que hace todavía puede pasar
; /DIR= por línea de comandos.
; La de bienvenida sí se muestra —viene apagada de fábrica en Inno 6— porque es donde
; se advierte lo de la cuenta de Windows. DisableDirPage tiene que ser «yes» y no
; «auto»: auto no evita la pregunta en la primera instalación, que es justo la que
; importa.
DisableWelcomePage=no
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
WizardStyle=modern
ShowLanguageDialog=no

; Evita que un doble clic nervioso abra dos instaladores a la vez.
SetupMutex=PapelSoftInstalador

SetupIconFile=..\src\Papeleria.App\Resources\Images\app.ico
UninstallDisplayIcon={app}\{#Ejecutable}
UninstallDisplayName={#NombreApp}

; Medido sobre este mismo ejecutable: sin comprimir 93,0 MB en 3 s; lzma2/fast 82,3 MB
; en 17 s; lzma2/max 81,5 MB en 19 s; ultra64 con SolidCompression 81,5 MB en 25 s.
; lzma2/max cuesta tres segundos más que fast y deja el instalador más pequeño que el
; .exe crudo. Solid no aporta nada con un solo archivo dentro.
Compression=lzma2/max
SolidCompression=no

OutputDir=..\publish\instalador
; Sin número de versión en el nombre: así el enlace
; https://github.com/JDProgramer802/papeleria_app/releases/latest/download/PapelSoft-Instalador.exe
; apunta siempre a la última versión y se puede mandar por WhatsApp una sola vez. La
; versión igual se ve en la pantalla de bienvenida del asistente.
OutputBaseFilename=PapelSoft-Instalador

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Messages]
; Los mensajes de Inno en español ya usan «equipo» y no «ordenador», así que solo se
; reescriben los cuatro que en su versión de fábrica no se entienden o no dicen lo que
; aquí hace falta decir.
WelcomeLabel2=Se va a instalar [name/ver] en este computador.%n%nImportante: instálelo desde la misma sesión de Windows que usa a diario. Si lo instala desde otra cuenta o con «Ejecutar como administrador», el programa abrirá una base de datos vacía y parecerá que perdió su información.
OnlyOnTheseArchitectures=PapelSoft necesita un Windows de 64 bits, y este equipo es de 32 bits.%n%nComuníquese con soporte antes de continuar.
WinVersionTooLowError=PapelSoft necesita Windows 10 o Windows 11. Este equipo tiene una versión anterior de Windows.
ConfirmUninstall=¿Desea desinstalar PapelSoft de este computador?%n%nSus ventas, su inventario, sus clientes y sus copias de seguridad NO se borran: quedan guardados en el equipo por si vuelve a instalarlo.
FinishedLabel=PapelSoft quedó instalado en este computador. Encontrará el icono en el escritorio y en el menú Inicio.

[Files]
; Un solo archivo, nombrado explícitamente. Nada de comodines sobre la carpeta de
; publicación: ahí quedan .pdb y restos de compilaciones anteriores, y si alguna vez se
; colara un Papeleria.dll al lado del .exe el actualizador se apagaría solo
; (ServicioActualizaciones.ComprobarViabilidad → NoEsEjecutablePublicado).
;
; Sin «ignoreversion» a propósito: el programa se autoactualiza, así que en {app} puede
; haber un ejecutable MÁS nuevo que el de este instalador. Forzar la sobrescritura
; dejaría un binario viejo corriendo contra una base de datos ya migrada hacia adelante,
; y eso no falla al arrancar: falla más tarde, en mitad de una venta.
Source: "{#Origen}"; DestDir: "{app}"; DestName: "{#Ejecutable}"

[Icons]
; Ninguno de los dos es opcional. El acceso directo del escritorio es lo que se pidió y
; es por donde el negocio va a abrir el programa todas las mañanas; ofrecerlo como
; casilla marcable solo sirve para que alguien lo desmarque sin querer y después no
; sepa cómo entrar. Al no quedar tareas, el asistente se salta una página entera.
Name: "{autodesktop}\{#NombreApp}"; Filename: "{app}\{#Ejecutable}"
Name: "{autoprograms}\{#NombreApp}"; Filename: "{app}\{#Ejecutable}"

[Run]
Filename: "{app}\{#Ejecutable}"; Description: "Abrir {#NombreApp} ahora"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
; El actualizador aparta el ejecutable viejo como «Papeleria.exe.anterior» hasta el
; siguiente arranque, y deja un archivo de sonda si el proceso muere justo cuando está
; comprobando si puede escribir en su carpeta. Ninguno de los dos lo instaló Inno, así
; que sin estas dos líneas quedan huérfanos y la carpeta no se puede borrar.
Type: files; Name: "{app}\*.anterior"
Type: files; Name: "{app}\.escritura_*.tmp"
Type: dirifempty; Name: "{app}"

[Code]

{ Impide que un instalador viejo pise una instalación más nueva.

  El escenario real: el cliente guarda el instalador en Descargas, meses después el
  programa ya se autoactualizó varias veces y él vuelve a ejecutar ese archivo «para
  repararlo». Sin esta comprobación se quedaría con un binario viejo contra una base de
  datos migrada, que es una avería silenciosa y difícil de diagnosticar por teléfono. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Ruta, VersionInstalada: String;
  Instalada, Entrante: Int64;
begin
  Result := '';

  Ruta := ExpandConstant('{app}\{#Ejecutable}');

  if not FileExists(Ruta) then
    Exit;

  if not GetPackedVersion(Ruta, Instalada) then
    Exit;

  if not StrToVersion('{#Version}.0', Entrante) then
    Exit;

  if ComparePackedVersion(Instalada, Entrante) > 0 then
  begin
    GetVersionNumbersString(Ruta, VersionInstalada);

    Result :=
      'Este computador ya tiene PapelSoft ' + VersionInstalada + ', que es más nuevo' + #13#10 +
      'que la versión {#Version} de este instalador.' + #13#10 + #13#10 +
      'Instalarlo dejaría el programa atrasado frente a su propia base de datos.' + #13#10 +
      'No hace falta hacer nada: PapelSoft se actualiza solo.';
  end;
end;

{ Al terminar de desinstalar se le recuerda al usuario que sus datos siguen ahí. Un
  desinstalador que se lleva la base de datos del negocio por su cuenta es un desastre;
  uno que se va en silencio deja al usuario creyendo que perdió todo y borrando
  carpetas a mano para «limpiar». }
procedure CurUninstallStepChanged(CurStep: TUninstallStep);
begin
  { UninstallSilent: sin esta guarda, una desinstalación desatendida se quedaría
    esperando para siempre a que alguien pulse Aceptar en un cuadro invisible. }
  if (CurStep = usPostUninstall) and (not UninstallSilent) then
  begin
    MsgBox(
      'PapelSoft se quitó de este computador.' + #13#10 + #13#10 +
      'Sus datos NO se borraron. La base de datos y las imágenes siguen en' + #13#10 +
      ExpandConstant('{localappdata}') + '\PapeleriaApp' + #13#10 + #13#10 +
      'Sus copias de seguridad están en la subcarpeta Backups, salvo que usted haya ' +
      'elegido otra ubicación desde Configuración.' + #13#10 + #13#10 +
      'Si vuelve a instalar PapelSoft en esta misma cuenta de Windows, encontrará su ' +
      'negocio como lo dejó.',
      mbInformation, MB_OK);
  end;
end;
