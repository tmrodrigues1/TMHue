; Instalador por usuário do TMHue. Compile pelo script packaging/build-installer.ps1.

#ifndef AppVersion
  #error "Defina AppVersion (por exemplo, /DAppVersion=1.0.0)."
#endif

#ifndef PublishDir
  #error "Defina PublishDir com o diretório do dotnet publish."
#endif

#ifndef OutputDir
  #error "Defina OutputDir para os artefatos da release."
#endif

#define AppName "TMHue"
#define AppPublisher "Thiago Rodrigues"
#define AppExeName "TMHue.exe"
#define AppId "{{D1A31DE3-98E1-4F5F-9C23-494AC78A30C5}"
#define AppYear GetDateTimeString('yyyy', '', '')

#ifdef SignTool
  #define FileFlags "ignoreversion sign"
#else
  #define FileFlags "ignoreversion"
#endif

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=TMHue-Setup-{#AppVersion}
SetupIconFile=..\..\src\TMHue.App\Assets\tray-icon.ico
UninstallDisplayIcon={app}\{#AppExeName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
DisableWelcomePage=no
DisableReadyPage=no
VersionInfoVersion={#AppVersion}
VersionInfoProductVersion={#AppVersion}
VersionInfoProductTextVersion={#AppVersion}
VersionInfoDescription={#AppName} Setup
VersionInfoTextVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoCopyright=Copyright (C) Thiago Rodrigues {#AppYear}
VersionInfoProductName={#AppName}

#ifdef SignTool
SignTool=production
SignedUninstaller=yes
#endif

[Languages]
Name: "portuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na área de trabalho"; GroupDescription: "Atalhos adicionais:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\{#AppExeName}"; DestDir: "{app}"; Flags: {#FileFlags}

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{autoprograms}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Abrir {#AppName}"; Flags: nowait postinstall skipifsilent

; The app registers itself under Run when the user enables "iniciar com o Windows"
; (TMHue.Windows/Startup/StartupService.cs). Declaring it here ensures Inno deletes
; the value on uninstall even though the installer itself never wrote it.
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "{#AppName}"; Flags: uninsdeletevalue

; Settings, color history and error logs all live under %LocalAppData%\TMHue
; (see TMHue.Windows/Persistence/AppPaths.cs). None of that is installed by [Files],
; so it must be removed explicitly or it survives uninstall.
[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\{#AppName}"
