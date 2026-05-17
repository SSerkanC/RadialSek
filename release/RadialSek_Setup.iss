#define MyAppName "Radial Sek"
#ifndef MyAppVersion
#define MyAppVersion "1.0.1"
#endif
#define MyAppPublisher "Radial Sek"
#define MyAppExeName "radial_sek.exe"

[Setup]
AppId={{7D7D6B4D-5A22-4A1F-9A39-7F4C7E6B5A11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Radial Sek
DefaultGroupName=Radial Sek
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=publish\sek_logo_final_2.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma
SolidCompression=yes
WizardStyle=modern
OutputDir=.
OutputBaseFilename=RadialSek_Setup_{#MyAppVersion}

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaustu kisayolu olustur"; GroupDescription: "Ek gorevler:"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Radial Sek"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\sek_logo_final_2.ico"
Name: "{autodesktop}\Radial Sek"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\sek_logo_final_2.ico"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Radial Sek'i baslat"; Flags: nowait postinstall skipifsilent
