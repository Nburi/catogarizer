; Inno Setup script for the Catogarizer installer.
; Build with build-installer.ps1 (publishes the app first, then compiles this).
; Installs per user (no admin rights needed) into %LOCALAPPDATA%\Programs\Catogarizer.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppName "Catogarizer"
#define PublishDir "..\src\Catogarizer.App\bin\Release\net10.0-windows\win-x64\publish"

[Setup]
AppId={{DB48CD4D-8642-44E4-A0C3-18CDEE5573BF}
AppName={#AppName}
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
AppPublisher=Catogarizer
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\Catogarizer.App\Assets\app.ico
UninstallDisplayIcon={app}\Catogarizer.exe
OutputDir=Output
OutputBaseFilename=Catogarizer-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Upgrading over a running copy: ask to close it instead of failing on a locked file.
CloseApplications=yes
RestartApplications=no

[Files]
Source: "{#PublishDir}\Catogarizer.App.exe"; DestDir: "{app}"; DestName: "Catogarizer.exe"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\Catogarizer.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\Catogarizer.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked

[Run]
Filename: "{app}\Catogarizer.exe"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent

; Categories and settings live in %LOCALAPPDATA%\Catogarizer and are deliberately kept on uninstall.
