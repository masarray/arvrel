#ifndef AppVersion
  #define AppVersion "0.1.0-beta.1"
#endif

#define AppName "ARVREL"
#define AppPublisher "Ari Sulistiono / masarray"
#define AppExeName "Arvrel.App.exe"

[Setup]
AppId={{9F3BF2F7-E86C-47DF-B8BE-2EA38AC16A50}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/masarray/arvrel
AppSupportURL=https://github.com/masarray/arvrel/issues
AppUpdatesURL=https://github.com/masarray/arvrel/releases
DefaultDirName={localappdata}\Programs\ARVREL
DefaultGroupName=ARVREL
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=..\LICENSE
InfoBeforeFile=..\COMMERCIAL-LICENSING.md
OutputDir=..\artifacts\release
OutputBaseFilename=ARVREL-Setup-v{#AppVersion}-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}
VersionInfoVersion=0.1.0.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=IEC 61850 Sampled Values virtual protection relay laboratory
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoCopyright=Copyright (C) 2026 Ari Sulistiono

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ARVREL"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\ARVREL"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch ARVREL"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
