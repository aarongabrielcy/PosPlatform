<#
.SYNOPSIS
    BASIC-INS-01: punto de entrada único para producir el instalador comercial de PosPlatform
    Desktop (Release / win-x64 / self-contained + Inno Setup -> un solo Setup.exe).

.DESCRIPTION
    Este script:
      1. Valida el endpoint de POS Cloud de producción (-PosCloudBaseUrl es OBLIGATORIO; nunca se
         usa localhost ni un valor inventado — REL-CLOUD-URL-01).
      2. Limpia su propio directorio de artefactos (artifacts\, ya ignorado por git).
      3. Compila y prueba la solución completa (dotnet build / dotnet test), salvo -SkipTests.
      4. Publica Pos.Desktop en Release/win-x64/self-contained a artifacts\publish\win-x64.
      5. Genera artifacts\publish\win-x64\appsettings.Production.json con el endpoint validado —
         NUNCA modifica el appsettings.json versionado (sección 13 de la tarea).
      6. Valida los artefactos publicados (sección 41): ejecutable presente, configuración de
         producción presente, sin archivos de desarrollo/cliente prohibidos, sin *.pdb.
      7. Compila installer\PosPlatform.iss con ISCC.exe (si está instalado) y calcula su SHA-256.
      8. Verifica que el árbol de código fuente no haya quedado sucio por el proceso (sección 13).

    NO ejecuta ningún comando Git de escritura (sección 61 de la tarea) — únicamente "git status
    --porcelain" de solo lectura, para probar la limpieza del árbol antes/después.

.PARAMETER PosCloudBaseUrl
    URL HTTPS real de POS Cloud para este build. Obligatorio. Se rechazan http, localhost/127.0.0.1/
    ::1 y (salvo -TestBuild) dominios de marcador de posición reservados (.invalid/.test/.example) —
    ver Pos.Application.Configuration.ReleasePackagingEndpointPolicy, la fuente de verdad probada por
    xunit que la función Test-ReleasePackagingEndpoint de este script replica (ver esa función).

.PARAMETER TestBuild
    Modo de validación técnica del instalador. Permite un endpoint de marcador de posición reservado
    (p. ej. https://posplatform-release-test.invalid) y marca inequívocamente el instalador resultante
    como TEST / NOT FOR DISTRIBUTION (sufijo "-TEST" en el nombre de archivo y en el nombre de
    aplicación mostrado por el asistente de instalación).

.PARAMETER SkipTests
    Omite "dotnet test" (el build de "dotnet build" siempre se ejecuta). Útil para iteración local
    del propio script; nunca usar para un build de distribución final.

.PARAMETER InnoCompilerPath
    Ruta explícita a ISCC.exe. Si se omite, el script busca en %LOCALAPPDATA%\Programs\Inno Setup 6
    (instalación per-user de winget), las ubicaciones estándar de instalación de Inno Setup 6/5 en
    Program Files/Program Files (x86), y finalmente en PATH.

.EXAMPLE
    .\eng\build-release.ps1 -PosCloudBaseUrl "https://api.posplatform.com"

.EXAMPLE
    .\eng\build-release.ps1 -PosCloudBaseUrl "https://posplatform-release-test.invalid" -TestBuild
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PosCloudBaseUrl,

    [switch]$TestBuild,

    [switch]$SkipTests,

    [string]$Configuration = "Release",

    [string]$RuntimeIdentifier = "win-x64",

    [string]$InnoCompilerPath
)

$ErrorActionPreference = "Stop"

# ==========================================================================================
# Rutas
# ==========================================================================================

$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repoRoot "PosPlatform.sln"
$desktopProjectPath = Join-Path $repoRoot "Pos.Desktop\Pos.Desktop.csproj"
$issPath = Join-Path $repoRoot "installer\PosPlatform.iss"

$artifactsDir = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsDir "publish\win-x64"
$installerDir = Join-Path $artifactsDir "installer"

# ==========================================================================================
# Validación del endpoint de POS Cloud (sección 9-11) — replica
# Pos.Application.Configuration.ReleasePackagingEndpointPolicy (Pos.Application.Tests la prueba con
# xunit). Ver el comentario de esa clase: duplicada aquí a propósito, no invocada por reflexión desde
# PowerShell, para no depender de resolver el grafo de dependencias transitivas de Pos.Application.dll
# desde un script de build. Cualquier cambio de regla en esa clase DEBE reflejarse aquí también.
# ==========================================================================================

function Test-ReleasePackagingEndpoint {
    param(
        [string]$BaseUrl,
        [bool]$IsTestBuild
    )

    if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
        return @{ Valid = $false; Reason = "No se especificó -PosCloudBaseUrl (missing)." }
    }

    $parsedUri = $null
    if (-not [Uri]::TryCreate($BaseUrl, [UriKind]::Absolute, [ref]$parsedUri)) {
        return @{ Valid = $false; Reason = "El valor no es una URL absoluta válida (invalid URL)." }
    }

    if ($parsedUri.Scheme -ne "https") {
        return @{ Valid = $false; Reason = "Debe usar HTTPS en un build de release (requires HTTPS)." }
    }

    if ($parsedUri.IsLoopback) {
        return @{ Valid = $false; Reason = "No puede apuntar a localhost/127.0.0.1/::1 en un build de release (localhost not allowed)." }
    }

    if (-not $IsTestBuild) {
        foreach ($suffix in @(".invalid", ".test", ".example")) {
            if ($parsedUri.Host.ToLowerInvariant().EndsWith($suffix)) {
                return @{
                    Valid  = $false
                    Reason = "Dominio de marcador de posición reservado ('$suffix') no permitido en build FINAL. Use -TestBuild para validación técnica del instalador (reserved placeholder)."
                }
            }
        }
    }

    return @{ Valid = $true; Reason = $null }
}

$isTestBuild = $TestBuild.IsPresent

Write-Host "==================================================================="
Write-Host "PosPlatform Desktop - Release Build"
Write-Host "Modo: $(if ($isTestBuild) { 'TEST (validación técnica del instalador)' } else { 'FINAL (distribución comercial)' })"
Write-Host "==================================================================="

$endpointCheck = Test-ReleasePackagingEndpoint -BaseUrl $PosCloudBaseUrl -IsTestBuild $isTestBuild
if (-not $endpointCheck.Valid) {
    throw "Validación de endpoint de release fallida (REL-CLOUD-URL-01): $($endpointCheck.Reason)"
}

Write-Host "==> Endpoint de POS Cloud validado: $PosCloudBaseUrl"

# ==========================================================================================
# Limpieza de artefactos propios (nunca toca nada fuera de artifacts\, ya ignorado por git)
# ==========================================================================================

if (Test-Path $artifactsDir) {
    Remove-Item -Path $artifactsDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $installerDir -Force | Out-Null

# Estado del árbol ANTES del build (sección 13: "a failed build must not leave the repository
# dirty" — probado comparando esto contra el estado al final del script). Solo lectura (git status
# --porcelain), ningún comando de escritura.
$gitStatusBefore = (& git -C $repoRoot status --porcelain) -join "`n"

# ==========================================================================================
# Build + pruebas (sección F de CLAUDE.md / sección 53 de la tarea)
# ==========================================================================================

Write-Host "==> dotnet build `"$solutionPath`" -c $Configuration --no-incremental"
& dotnet build $solutionPath -c $Configuration --no-incremental
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build falló (exit code $LASTEXITCODE)."
}

if (-not $SkipTests) {
    Write-Host "==> dotnet test `"$solutionPath`" -c $Configuration --no-build"
    & dotnet test $solutionPath -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test falló (exit code $LASTEXITCODE)."
    }
}
else {
    Write-Host "==> dotnet test OMITIDO (-SkipTests). No usar esta combinación para un build de distribución final."
}

# ==========================================================================================
# Publish (sección 5-7): Release/win-x64/self-contained, SIN PublishSingleFile/Trimmed/AOT
# (sección 6: WPF + EF Core + SQLite deben permanecer confiables; Inno Setup empaqueta los
# múltiples archivos resultantes en un único Setup.exe, no la publicación en sí).
# ==========================================================================================

Write-Host "==> dotnet publish `"$desktopProjectPath`" -c $Configuration -r $RuntimeIdentifier --self-contained true"
& dotnet publish $desktopProjectPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    -o $publishDir `
    /p:PublishSingleFile=false `
    /p:PublishTrimmed=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish falló (exit code $LASTEXITCODE)."
}

# Política de PDB (sección 33/34): no se empaquetan símbolos en el instalador público. El build de
# desarrollo conserva sus propios PDB normalmente en bin\/obj\, fuera de este directorio de publish.
$removedPdbs = Get-ChildItem -Path $publishDir -Filter "*.pdb" -Recurse -ErrorAction SilentlyContinue
if ($removedPdbs) {
    $removedPdbs | Remove-Item -Force
    Write-Host "==> $($removedPdbs.Count) archivo(s) .pdb eliminado(s) del publish (política de símbolos, sección 34)."
}

# ==========================================================================================
# Versión (sección 22): se lee del ejecutable YA PUBLICADO, nunca se duplica manualmente. La
# versión comercial autoritativa sigue siendo Directory.Build.props <Version> — el publish ya la
# aplicó al FileVersion del binario, así que leerla desde ahí es exactamente equivalente y evita
# tener que parsear el .props por separado.
# ==========================================================================================

$exePath = Join-Path $publishDir "Pos.Desktop.exe"
if (-not (Test-Path $exePath)) {
    throw "No se encontró el ejecutable publicado en $exePath."
}

$rawFileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath).FileVersion
if ([string]::IsNullOrWhiteSpace($rawFileVersion)) {
    throw "No fue posible leer FileVersion de $exePath."
}
$version = $rawFileVersion -replace '\.0$', ''

Write-Host "==> Versión detectada desde el ejecutable publicado: $version (FileVersion crudo: $rawFileVersion)"

# ==========================================================================================
# Configuración de release (sección 12/13): generada SOLO en staging/publish, jamás en el
# appsettings.json versionado. Host.CreateDefaultBuilder() ya carga
# "appsettings.{EnvironmentName}.json" con mayor precedencia que "appsettings.json" quedando
# EnvironmentName = "Production" por defecto en una máquina cliente sin DOTNET_ENVIRONMENT
# configurado — ver la prueba de regresión
# HostConfigurationFactoryTests.CreateBaseBuilderLoadsProductionAppsettingsOverBaseAppsettings.
# ==========================================================================================

$productionConfig = [ordered]@{
    Activation = [ordered]@{
        BaseUrl = $PosCloudBaseUrl
    }
}
$productionConfigPath = Join-Path $publishDir "appsettings.Production.json"
($productionConfig | ConvertTo-Json -Depth 5) | Set-Content -Path $productionConfigPath -Encoding utf8

Write-Host "==> appsettings.Production.json generado en el publish (no versionado)."

# ==========================================================================================
# Validación de release (sección 41) — falla ANTES de invocar Inno Setup.
# ==========================================================================================

function Test-ReleaseArtifacts {
    param(
        [string]$PublishDir
    )

    $validationErrors = New-Object System.Collections.Generic.List[string]

    if (-not (Test-Path (Join-Path $PublishDir "Pos.Desktop.exe"))) {
        $validationErrors.Add("Falta Pos.Desktop.exe en el publish.")
    }
    if (-not (Test-Path (Join-Path $PublishDir "appsettings.json"))) {
        $validationErrors.Add("Falta appsettings.json en el publish.")
    }
    if (-not (Test-Path (Join-Path $PublishDir "appsettings.Production.json"))) {
        $validationErrors.Add("Falta appsettings.Production.json generado en el publish.")
    }

    # Archivos de desarrollo/cliente que NUNCA deben viajar en el instalador (sección 33/51).
    $forbiddenPatterns = @(
        "*.Tests.dll",
        "appsettings.Local.json",
        "appsettings.Development*.json",
        "pos.db",
        "pos.db-shm",
        "pos.db-wal"
    )
    foreach ($pattern in $forbiddenPatterns) {
        $found = Get-ChildItem -Path $PublishDir -Filter $pattern -Recurse -ErrorAction SilentlyContinue
        if ($found) {
            $validationErrors.Add("Archivo(s) prohibido(s) encontrados en publish ($pattern): $($found.FullName -join ', ')")
        }
    }

    $pdbFound = Get-ChildItem -Path $PublishDir -Filter "*.pdb" -Recurse -ErrorAction SilentlyContinue
    if ($pdbFound) {
        $validationErrors.Add("Se encontraron archivos .pdb en publish (política sección 34: no empaquetar símbolos).")
    }

    # Ninguna carpeta de datos de cliente (Data\, Logs\, Activation\, Enforcement\) debe existir en
    # el publish: el instalador nunca debe enviar una base de datos precargada (sección 39).
    foreach ($forbiddenDir in @("Data", "Logs")) {
        if (Test-Path (Join-Path $PublishDir $forbiddenDir)) {
            $validationErrors.Add("El publish contiene una carpeta '$forbiddenDir' (posibles datos de cliente) — no debe existir.")
        }
    }

    return $validationErrors
}

$validationErrors = Test-ReleaseArtifacts -PublishDir $publishDir
if ($validationErrors.Count -gt 0) {
    foreach ($validationError in $validationErrors) {
        Write-Host "ERROR DE VALIDACIÓN: $validationError" -ForegroundColor Red
    }
    throw "La validación de artefactos de release falló (sección 41 de la tarea). Corrija antes de generar el instalador."
}

Write-Host "==> Validación de artefactos de release: OK."

# ==========================================================================================
# Inno Setup (sección 17-23)
# ==========================================================================================

$outputBaseFileName = if ($isTestBuild) { "PosPlatform-Setup-$version-TEST" } else { "PosPlatform-Setup-$version" }

function Find-InnoSetupCompiler {
    param([string]$OverridePath)

    if ($OverridePath) {
        if (Test-Path $OverridePath) {
            return $OverridePath
        }
        throw "InnoCompilerPath especificado no existe: $OverridePath"
    }

    $candidates = New-Object System.Collections.Generic.List[string]

    # Instalación per-user de winget (sin privilegios admin): winget install JRSoftware.InnoSetup
    # coloca ISCC.exe bajo %LOCALAPPDATA%\Programs\Inno Setup 6, fuera de Program Files. Se revisa
    # antes que las rutas de máquina porque es la ruta real observada en esta máquina de desarrollo.
    if ($env:LOCALAPPDATA) {
        $candidates.Add((Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"))
    }
    if ($env:ProgramFiles) {
        $candidates.Add((Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"))
    }
    if (${env:ProgramFiles(x86)}) {
        $candidates.Add((Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"))
        $candidates.Add((Join-Path ${env:ProgramFiles(x86)} "Inno Setup 5\ISCC.exe"))
    }

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $onPath = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($onPath) {
        return $onPath.Source
    }

    return $null
}

$isccPath = Find-InnoSetupCompiler -OverridePath $InnoCompilerPath
$installerExePath = $null
$installerSha256 = $null

if ($isccPath) {
    Write-Host "==> Compilando instalador con $isccPath"

    $issDefines = @(
        "/DMyAppVersion=$version",
        "/DMyPublishDir=$publishDir",
        "/DMyOutputDir=$installerDir",
        "/DMyOutputBaseFileName=$outputBaseFileName",
        "/DMyIsTestBuild=$(if ($isTestBuild) { '1' } else { '0' })"
    )

    & $isccPath @issDefines $issPath
    if ($LASTEXITCODE -ne 0) {
        throw "ISCC.exe falló (exit code $LASTEXITCODE)."
    }

    $installerExePath = Join-Path $installerDir "$outputBaseFileName.exe"
    if (-not (Test-Path $installerExePath)) {
        throw "ISCC.exe reportó éxito pero no se encontró el instalador esperado en $installerExePath."
    }

    $hash = Get-FileHash -Path $installerExePath -Algorithm SHA256
    $installerSha256 = $hash.Hash
    $hashLine = "$($hash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $installerExePath)"
    Set-Content -Path "$installerExePath.sha256" -Value $hashLine -Encoding ascii

    Write-Host "==> Instalador generado: $installerExePath"
    Write-Host "==> SHA-256: $installerSha256"
}
else {
    Write-Host ""
    Write-Host "INNO COMPILER ENVIRONMENT PENDING" -ForegroundColor Yellow
    Write-Host "No se encontró ISCC.exe. Instale Inno Setup 6 (https://jrsoftware.org/isdl.php) o pase -InnoCompilerPath." -ForegroundColor Yellow
    Write-Host "Comando equivalente una vez instalado:"
    Write-Host "  ISCC.exe /DMyAppVersion=$version /DMyPublishDir=`"$publishDir`" /DMyOutputDir=`"$installerDir`" /DMyOutputBaseFileName=$outputBaseFileName /DMyIsTestBuild=$(if ($isTestBuild) { '1' } else { '0' }) `"$issPath`""
    Write-Host ""
}

# ==========================================================================================
# Limpieza del árbol de código fuente (sección 13): un build fallido o exitoso nunca debe dejar
# cambios en archivos rastreados por git — solo lectura, ningún comando de escritura.
# ==========================================================================================

$gitStatusAfter = (& git -C $repoRoot status --porcelain) -join "`n"
if ($gitStatusBefore -ne $gitStatusAfter) {
    throw "El árbol de código fuente cambió durante el build de release (sección 13 de la tarea).`nAntes:`n$gitStatusBefore`nDespués:`n$gitStatusAfter"
}

Write-Host "==> Árbol de código fuente sin cambios (sección 13): OK."

# ==========================================================================================
# Resumen
# ==========================================================================================

Write-Host ""
Write-Host "==================================================================="
Write-Host "RESUMEN"
Write-Host "==================================================================="
Write-Host "Modo:                $(if ($isTestBuild) { 'TEST' } else { 'FINAL' })"
Write-Host "Versión:             $version"
Write-Host "Endpoint POS Cloud:  $PosCloudBaseUrl"
Write-Host "Publish:             $publishDir"
if ($installerExePath) {
    Write-Host "Instalador:          $installerExePath"
    Write-Host "SHA-256:             $installerSha256"
}
else {
    Write-Host "Instalador:          NO GENERADO (INNO COMPILER ENVIRONMENT PENDING)"
}
Write-Host "==================================================================="
