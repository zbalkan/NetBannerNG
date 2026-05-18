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

[Dirs]
Name: "{commonappdata}\{#MyProgramDataDir}"; Permissions: users-modify
Name: "{commonappdata}\{#MyProgramDataDir}\Logs"; Permissions: users-modify

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

; Default policy-backed settings, written on first install only.
Root: HKLM; Subkey: "{#MyPolicyRegistryKey}"; ValueType: string; ValueName: "ClassificationSelection"; ValueData: "NOT CONFIGURED - Classification not configured"; Flags: createvalueifdoesntexist
Root: HKLM; Subkey: "{#MyPolicyRegistryKey}"; ValueType: dword; ValueName: "CustomSettings"; ValueData: "{#DefaultCustomSettings}"; Flags: createvalueifdoesntexist
Root: HKLM; Subkey: "{#MyPolicyRegistryKey}"; ValueType: string; ValueName: "CustomBackgroundColor"; ValueData: "{#DefaultCustomBackgroundColor}"; Flags: createvalueifdoesntexist
Root: HKLM; Subkey: "{#MyPolicyRegistryKey}"; ValueType: string; ValueName: "CustomForeColor"; ValueData: "{#DefaultCustomForeColor}"; Flags: createvalueifdoesntexist
Root: HKLM; Subkey: "{#MyPolicyRegistryKey}"; ValueType: string; ValueName: "CustomDisplayText"; ValueData: ""; Flags: createvalueifdoesntexist
Root: HKLM; Subkey: "{#MyPolicyRegistryKey}"; ValueType: dword; ValueName: "InfoCon"; ValueData: "{#DefaultInfoCon}"; Flags: createvalueifdoesntexist
Root: HKLM; Subkey: "{#MyPolicyRegistryKey}"; ValueType: dword; ValueName: "FpCon"; ValueData: "{#DefaultFpCon}"; Flags: createvalueifdoesntexist
Root: HKLM; Subkey: "{#MyPolicyRegistryKey}"; ValueType: dword; ValueName: "CaveatsEnabled"; ValueData: "{#DefaultCaveatsEnabled}"; Flags: createvalueifdoesntexist
Root: HKLM; Subkey: "{#MyPolicyRegistryKey}"; ValueType: string; ValueName: "Caveats"; ValueData: ""; Flags: createvalueifdoesntexist
Root: HKLM; Subkey: "{#MyPolicyRegistryKey}"; ValueType: dword; ValueName: "BannerSize"; ValueData: "{#DefaultBannerSize}"; Flags: createvalueifdoesntexist
Root: HKLM; Subkey: "{#MyPolicyRegistryKey}"; ValueType: dword; ValueName: "DisableBorders"; ValueData: "{#DefaultDisableBorders}"; Flags: createvalueifdoesntexist
Root: HKLM; Subkey: "{#MyPolicyRegistryKey}"; ValueType: dword; ValueName: "ShowHostInformation"; ValueData: "{#DefaultShowHostInformation}"; Flags: createvalueifdoesntexist
Root: HKLM; Subkey: "{#MyPolicyRegistryKey}"; ValueType: dword; ValueName: "EnableBottomBanner"; ValueData: "{#DefaultEnableBottomBanner}"; Flags: createvalueifdoesntexist

[UninstallDelete]
; Remove machine-wide runtime state owned by NetBannerNG.
Type: filesandordirs; Name: "{commonappdata}\{#MyProgramDataDir}"

; Remove install directory if empty after uninstall.
Type: dirifempty; Name: "{app}"

[Code]

const
  // Explicit service-object DACL baseline for NetBannerNGWatchdog.
  //
  // Intention:
  // - Grant full service control to LocalSystem (SY) and Built-in Administrators (BA).
  // - Grant read-oriented service access to Authenticated Users (AU) only.
  //   This avoids broad non-admin service reconfiguration/deletion permissions.
  //
  // SDDL rights used:
  // - SY/BA: CCDCLCSWRPWPDTLOCRSDRCWDWO (full management set).
  // - AU:    CCLCSWLOCRRC (read/list/query style access).
  ServiceSecurityDescriptor =
    'D:(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;SY)' +
    '(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)' +
    '(A;;CCLCSWLOCRRC;;;AU)';

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