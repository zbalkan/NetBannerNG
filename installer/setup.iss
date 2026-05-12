#include "generated\NetBannerNG.BuildInfo.issinc"

[Setup]
AppId={{#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}

OutputDir=installer-output
OutputBaseFilename={#MyAppName}-{#MyAppVersion}-Setup

Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile={#MyInstallerIconFile}

PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyUiExeName}

CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; \
    Description: "Create a desktop shortcut"; \
    GroupDescription: "Additional icons:"; \
    Flags: unchecked

[Dirs]
Name: "{commonappdata}\{#MyProgramDataDir}"; Permissions: users-modify
Name: "{commonappdata}\{#MyProgramDataDir}\Logs"; Permissions: users-modify

[Files]
; WPF UI output.
; SDK-style net481 project output.
Source: "{#MyUiOutputDir}*"; \
    DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

; Watchdog service output.
; SDK-style net481 project output.
Source: "{#MyServiceOutputDir}*"; \
    DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; \
    Filename: "{app}\{#MyUiExeName}"

Name: "{autodesktop}\{#MyAppName}"; \
    Filename: "{app}\{#MyUiExeName}"; \
    Tasks: desktopicon

[Registry]
; Application-owned local registry key.
;
; This key is explicitly removed during uninstall in [Code].
; Do not delete GPO-owned policy keys here.
; Specifically, do not delete:
; HKLM\SOFTWARE\Policies\Microsoft\NetBanner

Root: HKLM; \
    Subkey: "{#MyAppRegistryKey}"; \
    ValueType: string; \
    ValueName: "InstallPath"; \
    ValueData: "{app}"

Root: HKLM; \
    Subkey: "{#MyAppRegistryKey}"; \
    ValueType: string; \
    ValueName: "Version"; \
    ValueData: "{#MyAppVersion}"

[Run]
; Service lifecycle is handled in [Code].
; This keeps upgrade behavior controlled:
; - stop before replacing binaries
; - create/configure after install
; - start after install

Filename: "{app}\{#MyUiExeName}"; \
    Description: "Launch {#MyAppName}"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove machine-wide runtime state owned by NetBannerNG.
; This deletes logs and ProgramData runtime files.
; Remove this entry if logs should survive uninstall.

Type: filesandordirs; Name: "{commonappdata}\{#MyProgramDataDir}"

; Remove install directory if empty after uninstall.

Type: dirifempty; Name: "{app}"

[Code]

function RunSc(Parameters: string): Integer;
var
  ResultCode: Integer;
begin
  Exec(
    ExpandConstant('{sys}\sc.exe'),
    Parameters,
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode
  );

  Result := ResultCode;
end;

procedure RunScChecked(Parameters: string; ErrorMessage: string);
var
  ResultCode: Integer;
begin
  ResultCode := RunSc(Parameters);

  if ResultCode <> 0 then
  begin
    MsgBox(
      ErrorMessage + #13#10 + 'sc.exe exit code: ' + IntToStr(ResultCode),
      mbError,
      MB_OK
    );

    Abort;
  end;
end;

function ServiceExists(ServiceName: string): Boolean;
var
  ResultCode: Integer;
begin
  Exec(
    ExpandConstant('{sys}\sc.exe'),
    'query "' + ServiceName + '"',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode
  );

  Result := ResultCode = 0;
end;

function ServiceQueryContains(ServiceName: string; Text: string): Boolean;
var
  ResultCode: Integer;
  TempFile: string;
  Command: string;
  Output: AnsiString;
begin
  Result := False;

  TempFile := ExpandConstant('{tmp}\sc-query-' + ServiceName + '.txt');

  Command :=
    '/C "' +
    '"' + ExpandConstant('{sys}\sc.exe') + '" query "' + ServiceName + '" > "' + TempFile + '" 2>&1' +
    '"';

  Exec(
    ExpandConstant('{cmd}'),
    Command,
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode
  );

  if LoadStringFromFile(TempFile, Output) then
  begin
    Result := Pos(Text, Output) > 0;
  end;
end;

function ServiceIsRunning(ServiceName: string): Boolean;
begin
  Result := ServiceQueryContains(ServiceName, 'RUNNING');
end;

function ServiceIsStopped(ServiceName: string): Boolean;
begin
  Result := ServiceQueryContains(ServiceName, 'STOPPED');
end;

procedure StopServiceIfNotStopped(ServiceName: string);
var
  I: Integer;
begin
  if not ServiceExists(ServiceName) then
    Exit;

  if not ServiceIsStopped(ServiceName) then
  begin
    RunSc('stop "' + ServiceName + '"');

    for I := 1 to 30 do
    begin
      if ServiceIsStopped(ServiceName) then
        Exit;

      Sleep(1000);
    end;
  end;
end;

procedure DeleteServiceIfExists(ServiceName: string);
var
  I: Integer;
begin
  if not ServiceExists(ServiceName) then
    Exit;

  StopServiceIfNotStopped(ServiceName);

  RunSc('delete "' + ServiceName + '"');

  for I := 1 to 10 do
  begin
    if not ServiceExists(ServiceName) then
      Exit;

    Sleep(1000);
  end;
end;

procedure InstallOrUpdateService();
var
  ServiceBinaryPath: string;
begin
  ServiceBinaryPath := ExpandConstant('{app}\{#MyServiceExeName}');

  if not ServiceExists('{#MyServiceName}') then
  begin
    RunScChecked(
      'create "{#MyServiceName}" ' +
      'binPath= "' + ServiceBinaryPath + '" ' +
      'DisplayName= "{#MyServiceDisplayName}" ' +
      'start= auto ' +
      'obj= "NT AUTHORITY\LocalService"',
      'Failed to create the NetBannerNG watchdog service.'
    );
  end
  else
  begin
    RunScChecked(
      'config "{#MyServiceName}" ' +
      'binPath= "' + ServiceBinaryPath + '" ' +
      'DisplayName= "{#MyServiceDisplayName}" ' +
      'start= auto ' +
      'obj= "NT AUTHORITY\LocalService"',
      'Failed to configure the NetBannerNG watchdog service.'
    );
  end;

  RunScChecked(
    'description "{#MyServiceName}" "{#MyServiceDescription}"',
    'Failed to set the NetBannerNG watchdog service description.'
  );

  RunScChecked(
    'failure "{#MyServiceName}" reset= 86400 actions= restart/60000/restart/60000/none/0',
    'Failed to configure NetBannerNG watchdog service recovery options.'
  );
end;

procedure StartServiceIfInstalled();
begin
  if ServiceExists('{#MyServiceName}') then
  begin
    if not ServiceIsRunning('{#MyServiceName}') then
    begin
      RunScChecked(
        'start "{#MyServiceName}"',
        'Failed to start the NetBannerNG watchdog service.'
      );
    end;
  end;
end;

procedure DeleteEventLogSourceRegistryKey();
var
  EventLogSourceKey: string;
begin
  EventLogSourceKey :=
    'SYSTEM\CurrentControlSet\Services\EventLog\{#MyEventLogName}\{#MyEventLogSource}';

  if RegKeyExists(HKLM, EventLogSourceKey) then
  begin
    RegDeleteKeyIncludingSubkeys(HKLM, EventLogSourceKey);
  end;
end;

procedure DeleteApplicationRegistryKey();
begin
  if RegKeyExists(HKLM, '{#MyAppRegistryKey}') then
  begin
    RegDeleteKeyIncludingSubkeys(HKLM, '{#MyAppRegistryKey}');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    StopServiceIfNotStopped('{#MyServiceName}');
  end;

  if CurStep = ssPostInstall then
  begin
    InstallOrUpdateService();
    StartServiceIfInstalled();
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    DeleteServiceIfExists('{#MyServiceName}');
    DeleteEventLogSourceRegistryKey();
    DeleteApplicationRegistryKey();
  end;
end;