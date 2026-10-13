; Inno Setup script. Build with installer/build-installer.ps1 (publishes first).
#define AppName "MonoFlixel"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

[Setup]
AppId={{8A3F6C52-4E1B-4D7A-9B0E-5C2D7E9F1A31}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Scott Chipman
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=..\dist
OutputBaseFilename=MonoFlixel-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=..\LICENSE
WizardStyle=modern
UninstallDisplayIcon={app}\MonoFlixelStarter.exe

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
; self-contained publish: the .NET runtime is inside, nothing else to install
Source: "..\dist\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\MonoFlixelStarter.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\MonoFlixelStarter.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\MonoFlixelStarter.exe"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
