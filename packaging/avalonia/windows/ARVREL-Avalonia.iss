#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "."
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif
#ifndef IconFile
  #define IconFile ""
#endif

[Setup]
AppId={{9F04E2A0-5F97-49DD-9C1C-B0E9092C55D4}
AppName=ARVREL
AppVersion={#AppVersion}
AppVerName=ARVREL {#AppVersion}
AppPublisher=masarray
AppPublisherURL=https://masarray.github.io/arvrel/
AppSupportURL=https://github.com/masarray/arvrel/issues
AppUpdatesURL=https://github.com/masarray/arvrel/releases
DefaultDirName={localappdata}\Programs\ARVREL-Avalonia
DefaultGroupName=ARVREL
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=ARVREL-Avalonia-v{#AppVersion}-win-x64-setup
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\Arvrel.Desktop.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
LicenseFile={#PublishDir}\LICENSE
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no
ChangesEnvironment=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\ARVREL"; Filename: "{app}\Arvrel.Desktop.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\ARVREL"; Filename: "{app}\Arvrel.Desktop.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Arvrel.Desktop.exe"; Description: "Launch ARVREL"; Flags: nowait postinstall skipifsilent
