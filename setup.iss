#define MyAppName "BuildCat"
#define MyAppVersion GetEnv("APP_VERSION")
#define MyAppPublisher "Karlmit"
#define MyAppURL "https://github.com/Karlmit/BuildCat"
#define MyAppExeName "BuildCat.exe"
#define MyPublishDir "BuildCat\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{A3F2D1C8-4E7B-4F9A-8C3D-1B5E2F6A0D4C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={localappdata}\{#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=BuildCat-{#MyAppVersion}-Setup
SetupIconFile=BuildCat\Assets\BuildCat.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startwithwindows"; Description: "Start BuildCat automatically with Windows"; Flags: checkedonce

[Files]
Source: "{#MyPublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userstartmenu}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BuildCat"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startwithwindows

[UninstallRun]
Filename: "{cmd}"; Parameters: "/c taskkill /im {#MyAppExeName} /f"; Flags: runhidden; RunOnceId: "KillBuildCat"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
