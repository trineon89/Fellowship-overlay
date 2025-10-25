#define MyAppName "Fellowship Overlay"
#define MyAppVersion "1.0.0"
#define MyAppExeName "Fellowship_overlay.exe"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputBaseFilename=Fellowship_overlay-Setup
Compression=lzma
SolidCompression=yes

[Files]
Source: "..\bin\Release\net8.0-windows\publish\win-x64\Fellowship_overlay.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
