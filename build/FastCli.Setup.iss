#define MyAppName "FastCli"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "FastCli Project"
#define MyAppExeName "FastCli.exe"

[Setup]
AppId={{C9A6FF7C-B47D-4DFE-9B91-7E2F0E4D7F29}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://example.invalid/fastcli
AppSupportURL=https://example.invalid/fastcli
AppUpdatesURL=https://example.invalid/fastcli
DefaultDirName={autopf}\FastCli
DefaultGroupName=FastCli
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\release
OutputBaseFilename=FastCli-Setup
SetupIconFile=..\assets\FastCli.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务:"; Flags: unchecked

[Files]
Source: "..\artifacts\release\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\FastCli"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\FastCli"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 FastCli"; Flags: nowait postinstall skipifsilent
