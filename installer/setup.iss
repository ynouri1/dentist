; Script Inno Setup — installeur Windows d'Ortho.
; Prérequis : publier d'abord l'application :
;   dotnet publish src/Ortho.UI -c Release -r win-x64 --self-contained -o installer/publish
; Puis compiler ce script avec Inno Setup 6 (ISCC.exe installer/setup.iss).
; NOTE : la signature de code (signtool) devra être ajoutée avant distribution
; aux cabinets (SmartScreen / Smart App Control bloquent les binaires non signés).

#define AppName "Ortho"
#define AppVersion "0.7.0"
#define AppPublisher "Ortho"
#define AppExeName "Ortho.UI.exe"

[Setup]
AppId={{8E1B9C64-52F1-4A9D-9C1B-ORTHO0000001}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=ortho-setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; Les données patient (%APPDATA%\Ortho) ne sont JAMAIS touchées par la
; désinstallation : elles appartiennent au cabinet.
