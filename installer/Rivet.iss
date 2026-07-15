; Inno Setup script for Rivet — per-user install, no admin required.
; Build: "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer\Rivet.iss
; (expects a self-contained publish in ..\publish — see installer\build.ps1)

#define MyAppName "Rivet"
#ifndef MyAppVersion
  #define MyAppVersion "0.2.2"
#endif
#define MyAppPublisher "Lunat1q"
#define MyAppURL "https://github.com/Lunat1q/Rivet"
#define MyAppExeName "Rivet.App.exe"

[Setup]
AppId={{7F3B9C1E-4A2D-4E77-9B6A-2C1D0F8E5A31}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=Rivet-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName} {#MyAppVersion}
SetupIconFile=..\src\Rivet.App\Assets\rivet.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
; The whole self-contained publish, minus the native runtimes for other operating systems
; (Whisper.net ships every RID; on Windows only win-x64 is used) and debug symbols.
; ignoreversion: overwrite every file, even one whose assembly version matches the installed
; copy. Without it Inno keeps the old file when only XAML/resources changed but the version did
; not — a same-version reinstall would ship stale UI.
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion; \
    Excludes: "*\linux-x64\*,*\linux-arm64\*,*\linux-arm\*,*\osx-x64\*,*\osx-arm64\*,*\android*,*\ios*,*.pdb"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; No skipifsilent: the in-app updater installs with /VERYSILENT, and we want Rivet to relaunch
; itself when that finishes so a silent update isn't left sitting closed. postinstall still shows
; the "Launch Rivet" checkbox on the finished page for a normal interactive install.
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall
