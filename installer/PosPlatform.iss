; PosPlatform Desktop — Inno Setup installer script
; BASIC-INS-01
;
; Este script se compila EXCLUSIVAMENTE desde eng\build-release.ps1, que pasa todos los valores
; #define listados abajo por línea de comandos (/D...). No hay una fuente de verdad de versión
; duplicada aquí (sección 22 de la tarea): el script de release ya la lee del ejecutable publicado.
; Compilar este .iss directamente con ISCC.exe sin esos /D usa los valores por defecto de abajo,
; útiles únicamente para validar la sintaxis del script, nunca para producir un instalador real.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef MyPublishDir
  #define MyPublishDir "..\artifacts\publish\win-x64"
#endif
#ifndef MyOutputDir
  #define MyOutputDir "..\artifacts\installer"
#endif
#ifndef MyOutputBaseFileName
  #define MyOutputBaseFileName "PosPlatform-Setup-" + MyAppVersion
#endif
#ifndef MyIsTestBuild
  #define MyIsTestBuild "0"
#endif

#define MyAppName "PosPlatform"
#define MyAppExeName "Pos.Desktop.exe"

; AppId estable (sección 30/31 de la tarea) — NO cambiar entre versiones. Es lo único que permite a
; un futuro instalador 1.0.1 reconocerse como upgrade in-place de esta misma instalación en vez de
; crear una entrada paralela en Aplicaciones instaladas. Generado una única vez (New-Guid) el
; 2026-08-23 para BASIC-INS-01; debe permanecer idéntico en todos los builds futuros de PosPlatform
; Desktop mientras el producto siga siendo el mismo.
; Nota de sintaxis Inno: "{" es el carácter que introduce una constante (p. ej. {app}, {autopf}).
; Para que el valor expandido de {#MyAppId} en la sección [Setup] se interprete como un GUID
; literal y no como un intento de constante desconocida, la llave de apertura debe escaparse
; duplicándola ("{{"). El GUID en sí (B7B6E1F0-9E3E-4E7A-9A9B-3C7C8B7C6B21) permanece sin cambios.
#define MyAppId "{{B7B6E1F0-9E3E-4E7A-9A9B-3C7C8B7C6B21}"

#if MyIsTestBuild == "1"
  #define MyDisplayAppName MyAppName + " (TEST BUILD - NOT FOR DISTRIBUTION)"
#else
  #define MyDisplayAppName MyAppName
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyDisplayAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyDisplayAppName} {#MyAppVersion}
VersionInfoVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}

; Instalación por-máquina estándar de 64 bits (sección 19/20/21): Program Files, no LocalAppData/
; Documents/Desktop — separa binarios de instalación de los datos mutables del cliente.
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyDisplayAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin

; x64 únicamente para Basic V1 (sección 21 de la tarea). No se empaqueta x86 ni ARM.
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

OutputDir={#MyOutputDir}
OutputBaseFilename={#MyOutputBaseFileName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyDisplayAppName}

; Cierre seguro de la aplicación durante reinstalación/actualización (sección 32): Inno Setup 6 usa
; Restart Manager para detectar procesos que mantienen abiertos los archivos que va a reemplazar y
; ofrece cerrarlos antes de continuar — evita corromper binarios sustituidos mientras la app sigue
; en ejecución, sin requerir ningún cambio de código en Pos.Desktop (p. ej. un mutex de instancia
; única, fuera del alcance de esta tarea de empaquetado).
CloseApplications=yes
RestartApplications=no

; REL-SIGN-01 (PENDIENTE — ver reporte final de BASIC-INS-01): no existe todavía un certificado de
; firma de código Authenticode real. Deliberadamente NO se genera ni usa un certificado autofirmado
; para simular una firma comercial (sección 43 de la tarea: "DO NOT generate a self-signed
; commercial release certificate and pretend it is production signing"). El día que exista un
; certificado real, la firma se inserta aquí mediante la directiva SignTool= (firma el instalador) y
; configurando signtool.exe sobre el propio publish antes de invocar ISCC.exe (firma el ejecutable).

; REL-EULA-01 (backlog — ver reporte final): no existe todavía un EULA/licencia comercial final.
; Deliberadamente no se agrega [Setup] LicenseFile= con texto legal inventado (sección 46 de la
; tarea). Esto NO bloquea la validación técnica del instalador.

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el Escritorio"; GroupDescription: "Accesos directos adicionales:"; Flags: unchecked

[Files]
; Todo el árbol de publish (Release/win-x64/self-contained), ya validado y depurado por
; eng\build-release.ps1 (sin *.pdb, sin datos de cliente, sin appsettings.Local.json/Development —
; ver Test-ReleaseArtifacts en ese script) ANTES de invocar ISCC.exe. Este script nunca referencia
; código fuente, proyectos de prueba ni datos de desarrollo directamente — solo ese árbol ya limpio.
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyDisplayAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyDisplayAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyDisplayAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Iniciar {#MyDisplayAppName}"; Flags: nowait postinstall skipifsilent

; ============================================================================================
; PRESERVACIÓN DE DATOS LOCALES — CRÍTICO (sección 28/29/50 de la tarea)
; ============================================================================================
; Deliberadamente NO existe una sección [UninstallDelete] en este script. Toda la persistencia de
; negocio de PosPlatform vive bajo %LOCALAPPDATA%\PosPlatform\ (fuera de {app}, que solo contiene
; binarios de instalación bajo Program Files):
;
;   %LOCALAPPDATA%\PosPlatform\Data\pos.db
;   %LOCALAPPDATA%\PosPlatform\Data\Activation\
;   %LOCALAPPDATA%\PosPlatform\Data\Enforcement\
;   %LOCALAPPDATA%\PosPlatform\Data\Config\
;   %LOCALAPPDATA%\PosPlatform\Data\ProductImages\
;   %LOCALAPPDATA%\PosPlatform\Data\Backups\
;   %LOCALAPPDATA%\PosPlatform\Logs\
;
; Ninguna de estas rutas es referenciada por este script en ninguna sección — ni [Files] (el
; instalador nunca escribe ahí, ver sección 40: "do not ship a prepopulated customer database") ni
; [UninstallDelete] (el desinstalador nunca las toca). Desinstalar solo elimina los binarios de
; aplicación y los accesos directos creados arriba; una reinstalación posterior sigue detectando la
; base de datos y configuración existentes sin ninguna acción adicional (sección 29/31: "reinstall ->
; existing local data must still be detected", soportado de forma nativa porque
; ApplicationPathProvider/LocalDatabaseInitializer ya resuelven estas mismas rutas en tiempo de
; ejecución sin conocimiento alguno del instalador). Basic V1 no ofrece ninguna opción de "borrar
; todos los datos de negocio" en el instalador (sección 29: "must never be an accidental uninstall
; checkbox").
