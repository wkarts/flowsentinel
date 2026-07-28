#ifndef AppVersion
  #define AppVersion "0.2.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-x64\desktop"
#endif
#ifndef ArchitecturesAllowed
  #define ArchitecturesAllowed "x64compatible"
#endif

[Setup]
AppId={{847A4336-190F-4D80-A548-649C540495D3}
AppName=FlowSentinel
AppVersion={#AppVersion}
AppPublisher=Wallace
DefaultDirName={autopf}\FlowSentinel
DefaultGroupName=FlowSentinel
OutputDir=..\artifacts\installer
OutputBaseFilename=FlowSentinel-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed={#ArchitecturesAllowed}
#ifdef ArchitecturesInstallIn64BitMode
ArchitecturesInstallIn64BitMode={#ArchitecturesInstallIn64BitMode}
#endif
UninstallDisplayIcon={app}\FlowSentinel.exe
SetupIconFile=..\src\FlowSentinel.Desktop\Assets\FlowSentinel.ico

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\FlowSentinel"; Filename: "{app}\FlowSentinel.exe"
Name: "{autodesktop}\FlowSentinel"; Filename: "{app}\FlowSentinel.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos adicionais:"
Name: "autostart"; Description: "Iniciar com o Windows"; GroupDescription: "Inicialização:"

[Registry]
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "FlowSentinel"; ValueData: """{app}\FlowSentinel.exe"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\FlowSentinel.exe"; Description: "Executar FlowSentinel"; Flags: nowait postinstall skipifsilent
