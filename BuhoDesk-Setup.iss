; BuhoDesk Setup Script for Inno Setup
; This creates a professional Windows installer

#define MyAppName "BuhoDesk"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "BuhoDesk Team"
#define MyAppURL "https://github.com/your-repo/buhodesk"
#define MyAppExeName "BuhoServer.exe"
#define MyAppClientExeName "BuhoClient.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE.txt
OutputDir=Output
OutputBaseFilename=BuhoDesk-Setup-v{#MyAppVersion}

Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Messages]
WelcomeLabel1=Welcome to the {#MyAppName} Setup Wizard
WelcomeLabel2=This will install {#MyAppName} {#MyAppVersion} on your computer.%n%nIt is recommended that you close all other applications before continuing.
FinishedLabel=Setup has finished installing {#MyAppName} on your computer. The application may be launched by selecting the installed shortcuts.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Server files
Source: "Installer\BuhoServer.exe"; DestDir: "{app}\Server"; Flags: ignoreversion
Source: "Installer\*"; DestDir: "{app}\Server"; Flags: ignoreversion recursesubdirs createallsubdirs

; Client files
Source: "Installer\BuhoClient.exe"; DestDir: "{app}\Client"; Flags: ignoreversion
Source: "Installer\*"; DestDir: "{app}\Client"; Flags: ignoreversion recursesubdirs createallsubdirs

; Documentation
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion

; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\{#MyAppName} Server"; Filename: "{app}\Server\{#MyAppExeName}"
Name: "{group}\{#MyAppName} Client"; Filename: "{app}\Client\{#MyAppClientExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName} Server"; Filename: "{app}\Server\{#MyAppExeName}"; Tasks: desktopicon
Name: "{autodesktop}\{#MyAppName} Client"; Filename: "{app}\Client\{#MyAppClientExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName} Server"; Filename: "{app}\Server\{#MyAppExeName}"; Tasks: quicklaunchicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName} Client"; Filename: "{app}\Client\{#MyAppClientExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\Server\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')} Server}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\Client\{#MyAppClientExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')} Client}"; Flags: nowait postinstall skipifsilent

[Registry]
; Add firewall rules
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules"; ValueType: string; ValueName: "BuhoDesk-Server-TCP-In"; ValueData: "v2.30|Action=Allow|Active=TRUE|Dir=In|Protocol=6|LPort=8080|App={app}\Server\{#MyAppExeName}|Name=BuhoDesk Server TCP Inbound|Desc=BuhoDesk Server TCP Inbound Rule|"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules"; ValueType: string; ValueName: "BuhoDesk-Server-UDP-In"; ValueData: "v2.30|Action=Allow|Active=TRUE|Dir=In|Protocol=17|LPort=8081|App={app}\Server\{#MyAppExeName}|Name=BuhoDesk Server UDP Inbound|Desc=BuhoDesk Server UDP Inbound Rule|"; Flags: uninsdeletevalue

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  // Check if .NET 9.0 is installed (for non-self-contained version)
  // For self-contained, this check is not needed
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    // Create firewall rules using netsh
    Exec('netsh', 'advfirewall firewall add rule name="BuhoDesk Server TCP" dir=in action=allow protocol=TCP localport=8080', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('netsh', 'advfirewall firewall add rule name="BuhoDesk Server UDP" dir=in action=allow protocol=UDP localport=8081', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
