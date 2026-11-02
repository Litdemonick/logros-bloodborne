; Instalador de Logros Bloodborne (para bbport). No incluye nada del juego.
; Se compila con build.ps1, que pasa la versión: ISCC /DVersion=x.y.z
#define Nombre "Logros Bloodborne"
#ifndef Version
  #define Version "1.0.0"
#endif
#define Exe "Logros Bloodborne.exe"

[Setup]
AppId={{6B2F8E4A-3C1D-4B7E-9A51-B10DB0E7A0C1}
AppName={#Nombre}
AppVersion={#Version}
AppVerName={#Nombre} {#Version}
AppPublisher=Litdemonick
AppPublisherURL=https://github.com/Litdemonick/logros-bloodborne
AppSupportURL=https://github.com/Litdemonick/logros-bloodborne/issues
DefaultDirName={localappdata}\Programs\{#Nombre}
DefaultGroupName={#Nombre}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=Instalar-Logros-Bloodborne
SetupIconFile=..\assets\icono.ico
UninstallDisplayIcon={app}\{#Exe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
RestartApplications=no
ShowLanguageDialog=no

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Messages]
es.WelcomeLabel2=Esto va a instalar [name/ver].%n%nMuestra tus trofeos de Bloodborne (bbport) sobre el juego mientras jugás, con sonido, y tiene una lista con todo tu progreso.%n%nSe conecta con tu bbport y queda en segundo plano. Solo lee el registro de trofeos: no toca tus partidas ni los archivos del juego.

[Tasks]
Name: "inicio"; Description: "Iniciar con Windows, en segundo plano (recomendado, así nunca te perdés un trofeo)"; GroupDescription: "Opciones:"
Name: "escritorio"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Opciones:"

[Files]
Source: "..\bin\{#Exe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "LEEME.txt"; DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
Name: "{autoprograms}\{#Nombre}"; Filename: "{app}\{#Exe}"; Comment: "Tus trofeos de Bloodborne"
Name: "{autodesktop}\{#Nombre}"; Filename: "{app}\{#Exe}"; Tasks: escritorio; Comment: "Tus trofeos de Bloodborne"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "LogrosBloodborne"; ValueData: """{app}\{#Exe}"" --bandeja"; Tasks: inicio; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#Exe}"; Description: "Abrir {#Nombre} ahora"; Flags: nowait postinstall skipifsilent
; actualizar encima conserva carpeta y ajustes (mismo AppId); ajustes y trofeos viven en %APPDATA%

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM ""{#Exe}"""; Flags: runhidden; RunOnceId: "CerrarLogros"

[UninstallDelete]
; Se borran los ajustes; se conservan los respaldos de trofeos y el tiempo registrado
Type: files; Name: "{userappdata}\LogrosBloodborne\ajustes.ini*"
Type: files; Name: "{userappdata}\LogrosBloodborne\vistos-*"
Type: files; Name: "{userappdata}\LogrosBloodborne\errores*.log"

[Code]
var
  EstabaAbierto: Boolean;

// Instalación nueva o actualización: cerrar la copia que esté corriendo en segundo plano
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Codigo: Integer;
begin
  EstabaAbierto := Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM "{#Exe}"', '', SW_HIDE, ewWaitUntilTerminated, Codigo) and (Codigo = 0);
  Result := '';
end;

// Actualización silenciosa (desde el programa usa /RELANZAR=1): dejarlo otra vez en segundo plano
procedure CurStepChanged(CurStep: TSetupStep);
var
  Codigo: Integer;
begin
  if (CurStep = ssPostInstall) and WizardSilent and (EstabaAbierto or (ExpandConstant('{param:RELANZAR|0}') = '1')) then
    Exec(ExpandConstant('{app}\{#Exe}'), '--bandeja', '', SW_SHOWNORMAL, ewNoWait, Codigo);
end;

// Al desinstalar, quitar también el inicio automático si se activó desde el programa
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'LogrosBloodborne');
end;
