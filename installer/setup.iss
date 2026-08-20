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
DisableProgramGroupPage=yes

OutputDir=installer-output
OutputBaseFilename={#MyAppName}-{#MyAppVersion}-Setup

Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile={#MyInstallerIconFile}

PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Uses the installer's custom icon embedded directly inside the uninstaller binary
UninstallDisplayIcon={uninstallexe}

CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; UI (WPF) application output. The watchdog launches {app}\{#MyUiExeName},
; so the GUI must ship alongside the service in the install directory.
Source: "{#MyUiOutputDir}*"; \
    DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

; Watchdog service output.
; SDK-style net481 project output.
Source: "{#MyServiceOutputDir}*"; \
    DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; Do not delete GPO-owned policy keys here.

; Event log source for {#MyEventLogSource}. The EventMessageFile must point
; to a message-resource DLL that contains an entry for every event ID we
; raise; otherwise Event Viewer renders "the description for Event ID ...
; cannot be found" (and, for IDs that collide with Win32 error codes, the
; matching kernel32 error text). EventLogMessages.dll that ships with the
; in-box .NET Framework 4 holds 65,536 "%1" templates, which lets WriteEntry
; render its message text verbatim for any event ID we use. Values are
; rewritten on every install so an upgrade repairs a key that an older
; build left pointing at the wrong file. Uninstall is handled by
; DeleteEventLogSourceRegistryKey below.
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Services\EventLog\{#MyEventLogName}\{#MyEventLogSource}"; ValueType: expandsz; ValueName: "EventMessageFile"; ValueData: "%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\EventLogMessages.dll"
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Services\EventLog\{#MyEventLogName}\{#MyEventLogSource}"; ValueType: dword; ValueName: "TypesSupported"; ValueData: "7"

; Product defaults are compiled into the application. The Policy key is reserved
; for administrator or GPO-managed settings and is intentionally not created,
; modified, or removed by this installer.

[UninstallDelete]
; Remove machine-wide runtime state owned by NetBannerNG.
Type: filesandordirs; Name: "{commonappdata}\{#MyProgramDataDir}"

; Remove install directory if empty after uninstall.
Type: dirifempty; Name: "{app}"

[Code]

const
  // Explicit service-object DACL baseline for NetBannerNGWatchdog.
  //
  // Only LocalSystem and Built-in Administrators may manage this LocalSystem
  // service. No standard-user service access is required by the product.
  ServiceSecurityDescriptor =
    'D:(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;SY)' +
    '(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)';

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
begin
  Result := RunSc('query "' + ServiceName + '"') = 0;
end;

function ServiceIsAbsent(ServiceName: string): Boolean;
begin
  // ERROR_SERVICE_DOES_NOT_EXIST. A service marked for deletion returns a
  // different error and must not be treated as safely removed.
  Result := RunSc('query "' + ServiceName + '"') = 1060;
end;

function ServiceIsPendingDeletion(ServiceName: string): Boolean;
begin
  // ERROR_SERVICE_MARKED_FOR_DELETE. This is an expected transient state
  // after sc.exe delete while another process holds a service handle.
  Result := RunSc('query "' + ServiceName + '"') = 1072;
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

function ServiceIsStopPending(ServiceName: string): Boolean;
begin
  Result := ServiceQueryContains(ServiceName, 'STOP_PENDING');
end;

procedure StopServiceIfNotStopped(ServiceName: string);
var
  I: Integer;
  ResultCode: Integer;
begin
  if ServiceIsAbsent(ServiceName) or ServiceIsStopped(ServiceName) then
    Exit;

  ResultCode := RunSc('stop "' + ServiceName + '"');
  if (ResultCode <> 0) and not ServiceIsStopped(ServiceName) then
  begin
    MsgBox(
      'Failed to stop the NetBannerNG watchdog service.' + #13#10 +
      'sc.exe exit code: ' + IntToStr(ResultCode),
      mbError,
      MB_OK
    );
    Abort;
  end;

  for I := 1 to 30 do
  begin
    if ServiceIsStopped(ServiceName) then
      Exit;

    Sleep(1000);
  end;

  MsgBox(
    'Timed out waiting for the NetBannerNG watchdog service to stop.',
    mbError,
    MB_OK
  );
  Abort;
end;

procedure DeleteServiceIfExists(ServiceName: string);
var
  I: Integer;
  ResultCode: Integer;
begin
  if ServiceIsAbsent(ServiceName) then
    Exit;

  if ServiceExists(ServiceName) then
    StopServiceIfNotStopped(ServiceName);

  if not ServiceIsPendingDeletion(ServiceName) then
  begin
    ResultCode := RunSc('delete "' + ServiceName + '"');
    if (ResultCode <> 0) and not ServiceIsAbsent(ServiceName) and not ServiceIsPendingDeletion(ServiceName) then
    begin
      MsgBox(
        'Failed to delete the NetBannerNG watchdog service.' + #13#10 +
        'sc.exe exit code: ' + IntToStr(ResultCode),
        mbError,
        MB_OK
      );
      Abort;
    end;
  end;

  for I := 1 to 30 do
  begin
    if ServiceIsAbsent(ServiceName) then
      Exit;

    Sleep(1000);
  end;

  MsgBox(
    'The NetBannerNG watchdog service is still pending deletion. Close any open service-management tools and retry the operation.',
    mbError,
    MB_OK
  );
  Abort;
end;

procedure InstallOrUpdateService();
var
  ServiceBinaryPath: string;
begin
  ServiceBinaryPath := ExpandConstant('{app}\{#MyServiceExeName}');

  // The watchdog launches the GUI in the active console session via
  // WTSQueryUserToken + CreateProcessAsUser. WTSQueryUserToken requires
  // SE_TCB_NAME ("Act as part of the operating system"), which only the
  // LocalSystem account holds — LocalService fails with Win32 error 1314
  // (ERROR_PRIVILEGE_NOT_HELD). Run the service as LocalSystem so the
  // token query succeeds.
  if not ServiceExists('{#MyServiceName}') then
  begin
    RunScChecked(
      'create "{#MyServiceName}" ' +
      'binPath= "\"' + ServiceBinaryPath + '\"" ' +
      'DisplayName= "{#MyServiceDisplayName}" ' +
      'start= auto ' +
      'obj= "LocalSystem"',
      'Failed to create the NetBannerNG watchdog service.'
    );
  end
  else
  begin
    RunScChecked(
      'config "{#MyServiceName}" ' +
      'binPath= "\"' + ServiceBinaryPath + '\"" ' +
      'DisplayName= "{#MyServiceDisplayName}" ' +
      'start= auto ' +
      'obj= "LocalSystem"',
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

  RunScChecked(
    'sdset "{#MyServiceName}" "' + ServiceSecurityDescriptor + '"',
    'Failed to configure NetBannerNG watchdog service permissions.'
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
  end;
end;