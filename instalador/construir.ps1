<#
    Construye el instalador de PapelSoft de punta a punta.

        powershell -ExecutionPolicy Bypass -File instalador\construir.ps1

    Hace tres cosas en orden: lee la versión del proyecto, publica el ejecutable
    autónomo y lo empaqueta con Inno Setup. La versión no se escribe en ningún otro
    sitio a mano, justamente para que el instalador no pueda decir una cosa y el
    ejecutable otra.

    Parámetros:
        -SoloEmpaquetar   Salta la publicación y empaqueta el .exe que ya esté hecho.
#>

[CmdletBinding()]
param(
    [switch] $SoloEmpaquetar
)

$ErrorActionPreference = 'Stop'

$raiz       = Split-Path -Parent $PSScriptRoot
$proyecto   = Join-Path $raiz 'src\Papeleria.App\Papeleria.App.csproj'
$publicado  = Join-Path $raiz 'publish\win-x64\Papeleria.exe'
$guion      = Join-Path $PSScriptRoot 'PapelSoft.iss'
$destino    = Join-Path $raiz 'publish\instalador'

# ── 1. La versión, tomada del proyecto ────────────────────────────────────────
[xml] $csproj = Get-Content $proyecto
$version = ($csproj.Project.PropertyGroup.Version | Where-Object { $_ }) | Select-Object -First 1

if (-not $version) {
    throw "No se pudo leer <Version> de $proyecto"
}

Write-Host "PapelSoft $version" -ForegroundColor Cyan

# ── 2. El ejecutable autónomo ─────────────────────────────────────────────────
if (-not $SoloEmpaquetar) {
    Write-Host 'Publicando el ejecutable...' -ForegroundColor DarkGray

    & dotnet publish $proyecto -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
        -o (Join-Path $raiz 'publish\win-x64') --nologo -v quiet

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish falló con código $LASTEXITCODE" }
}

if (-not (Test-Path $publicado)) {
    throw "No existe $publicado. Ejecute sin -SoloEmpaquetar."
}

$mb = [math]::Round((Get-Item $publicado).Length / 1MB, 1)
Write-Host "  Papeleria.exe -> $mb MB" -ForegroundColor DarkGray

# ── 3. El compilador de Inno Setup ────────────────────────────────────────────
# Se instala con:  winget install --id JRSoftware.InnoSetup --exact
$candidatos = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
)

$iscc = $candidatos | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw @"
No se encontró ISCC.exe (el compilador de Inno Setup 6).
Instálelo con:  winget install --id JRSoftware.InnoSetup --exact
"@
}

# ── 4. El instalador ──────────────────────────────────────────────────────────
Write-Host 'Empaquetando el instalador...' -ForegroundColor DarkGray

New-Item -ItemType Directory -Force -Path $destino | Out-Null

$salida = & $iscc $guion "/DVersion=$version" "/DOrigen=$publicado" /Q 2>&1

if ($LASTEXITCODE -ne 0) {
    $salida | ForEach-Object { Write-Host $_ }
    throw "Inno Setup falló con código $LASTEXITCODE"
}

$instalador = Join-Path $destino 'PapelSoft-Instalador.exe'

if (-not (Test-Path $instalador)) {
    throw "Inno Setup terminó bien pero no dejó $instalador"
}

# ── 5. Que los dos ejecutables no se puedan confundir ─────────────────────────
# El actualizador que corre en casa del cliente descarga un .exe de la publicación y lo
# renombra encima del programa. Su última defensa es mirar qué dice ser el archivo
# (ServicioActualizaciones.EsElPrograma), así que el instalador TIENE que declararse
# distinto. Hasta la versión 2.2.0 no lo hacía: los dos decían llamarse «PapelSoft», y
# un instalador descargado por error habría pasado el filtro sin despeinarse.
$productoApp        = (Get-Item $publicado).VersionInfo.ProductName.Trim()
$productoInstalador = (Get-Item $instalador).VersionInfo.ProductName.Trim()

if ($productoApp -cne 'PapelSoft') {
    throw "El ejecutable dice ser '$productoApp' y debería decir 'PapelSoft'. Revise <Product> en Papeleria.App.csproj."
}

if ($productoInstalador -ceq 'PapelSoft') {
    throw "El instalador se identifica igual que el programa. Revise VersionInfoProductName en PapelSoft.iss."
}

$mbInstalador = [math]::Round((Get-Item $instalador).Length / 1MB, 1)

Write-Host ''
Write-Host "  programa   se identifica como '$productoApp'" -ForegroundColor DarkGray
Write-Host "  instalador se identifica como '$productoInstalador'" -ForegroundColor DarkGray
Write-Host ''
Write-Host "Listo: $instalador ($mbInstalador MB)" -ForegroundColor Green
