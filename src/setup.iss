#define MyAppName "NetBannerNG"
#define MyAppSvcDisplayName "NetBannerNG Service"
#define MyAppVersion "1.0"
#define MyAppPublisher "Zafer Balkan"
#define MyAppURL "https://github.com/zbalkan/NetBannerNG"
#define MyAppExeName "NetBannerNG.exe"
#define MyAppSvcExeName "NetBannerNG.Service.exe"
#define MyAppSvcShortName "netbannerng"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{77A05D67-DF35-49F7-BE76-1E911BCE0FFB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\NetBannerNG
DisableDirPage=yes
DisableProgramGroupPage=yes
; Uncomment the following line to run in non administrative install mode (install for current user only.)
;PrivilegesRequired=lowest
OutputBaseFilename=netbannerng-setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SetupIconFile=publish\DeltaZulu - dark.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\*.exe"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "publish\*.cfg"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: {sys}\sc.exe; Parameters: "create ""{#MyAppSvcShortName}"" start= auto binPath= ""{app}\{#MyAppSvcExeName}"" displayname= ""{#MyAppSvcDisplayName}""" ; Flags: runhidden
Filename: {sys}\sc.exe; Parameters: "start ""{#MyAppSvcShortName}""" ; Flags: runhidden

[UninstallRun]
Filename: {sys}\sc.exe; Parameters: "stop ""{#MyAppSvcShortName}""" ; Flags: runhidden
Filename: {sys}\sc.exe; Parameters: "delete ""{#MyAppSvcShortName}""" ; Flags: runhidden
