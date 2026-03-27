#define MyAppName "FastCli"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.8"
#endif
#define MyAppPublisher "FastCli Project"
#define MyAppExeName "FastCli.exe"
#ifndef MyDotNetRuntimePrimaryUrl
  #define MyDotNetRuntimePrimaryUrl "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"
#endif
#ifndef MyDotNetRuntimeSecondaryUrl
  #define MyDotNetRuntimeSecondaryUrl "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.25/windowsdesktop-runtime-8.0.25-win-x64.exe"
#endif
#ifndef MyDotNetRuntimeMirrorUrl
  #define MyDotNetRuntimeMirrorUrl "https://github.com/mostbean-cn/fast-cli/releases/download/runtime-cache/windowsdesktop-runtime-8.0.25-win-x64.exe"
#endif
#ifndef MyAppSourceDir
  #define MyAppSourceDir "..\artifacts\release-setup"
#endif
#ifndef MyOutputDir
  #define MyOutputDir "..\artifacts\release-setup"
#endif

[Setup]
AppId={{3D44F065-46C7-4CB5-8D2C-46E246BD50A7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/mostbean-cn/fast-cli
AppSupportURL=https://github.com/mostbean-cn/fast-cli
AppUpdatesURL=https://github.com/mostbean-cn/fast-cli/releases
DefaultDirName={autopf}\FastCli
DisableDirPage=no
DefaultGroupName=FastCli
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#MyOutputDir}
OutputBaseFilename={#MyAppName}-Setup-v{#MyAppVersion}
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
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "FastCli-Setup-*.exe"

[Icons]
Name: "{autoprograms}\FastCli"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\FastCli"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 FastCli"; Flags: nowait postinstall skipifsilent; Check: CanLaunchFastCliAfterInstall

[Code]
const
  DotNetDesktopRuntimePrimaryUrl = '{#MyDotNetRuntimePrimaryUrl}';
  DotNetDesktopRuntimeSecondaryUrl = '{#MyDotNetRuntimeSecondaryUrl}';
  DotNetDesktopRuntimeMirrorUrl = '{#MyDotNetRuntimeMirrorUrl}';
  DotNetInstallerFileName = 'windowsdesktop-runtime-8-win-x64.exe';
  FileAttributeDirectory = $10;

var
  DownloadPage: TDownloadWizardPage;
  InstallRuntimePage: TOutputMarqueeProgressWizardPage;
  DotNetInstalledDuringSetup: Boolean;
  DotNetRequiresRestart: Boolean;
  CurrentDownloadSourceName: string;

function FormatBytes(Value: Int64): string;
begin
  if Value >= 1024 * 1024 then
  begin
    Result := Format('%.1f MB', [Value / 1024.0 / 1024.0]);
  end
  else if Value >= 1024 then
  begin
    Result := Format('%.1f KB', [Value / 1024.0]);
  end
  else
  begin
    Result := IntToStr(Value) + ' B';
  end;
end;

function GetRuntimeSourceName(DownloadUrl: string): string;
begin
  if DownloadUrl = DotNetDesktopRuntimePrimaryUrl then
  begin
    Result := 'Microsoft 官方短链 (aka.ms)';
  end
  else if DownloadUrl = DotNetDesktopRuntimeSecondaryUrl then
  begin
    Result := 'Microsoft 官方直链';
  end
  else
  begin
    Result := 'GitHub Releases 镜像';
  end;
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
var
  Percent: Integer;
  ProgressText: string;
begin
  if ProgressMax > 0 then
  begin
    Percent := Integer((Progress * 100) div ProgressMax); ProgressText := Format('来源：%s'#13#10'文件：%s'#13#10'进度：%d%% (%s / %s)', [CurrentDownloadSourceName, FileName, Percent, FormatBytes(Progress), FormatBytes(ProgressMax)]);
  end
  else
  begin
    ProgressText := Format('来源：%s'#13#10'文件：%s'#13#10'已下载：%s', [CurrentDownloadSourceName, FileName, FormatBytes(Progress)]);
  end;

  DownloadPage.SetText('检测到未安装 .NET Desktop Runtime 8', ProgressText);
  Result := True;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(
    '下载 .NET Desktop Runtime 8',
    '安装 FastCli 前需要先准备 .NET 桌面运行环境。',
    @OnDownloadProgress);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;

  InstallRuntimePage := CreateOutputMarqueeProgressPage(
    '安装 .NET Desktop Runtime 8',
    '正在安装 .NET 桌面运行环境，请稍候。');
end;

function HasDesktopRuntime8(): Boolean;
var
  FindRec: TFindRec;
  SearchPattern: string;
begin
  Result := False;
  SearchPattern := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*');

  if not FindFirst(SearchPattern, FindRec) then
  begin
    exit;
  end;

  try
    repeat
      if (FindRec.Attributes and FileAttributeDirectory) <> 0 then
      begin
        Result := True;
        break;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function TryDownloadRuntimeInstaller(DownloadUrl: string; InstallerPath: string): Boolean;
begin
  if DownloadUrl = '' then
  begin
    Result := False;
    exit;
  end;

  if FileExists(InstallerPath) then
  begin
    DeleteFile(InstallerPath);
  end;

  CurrentDownloadSourceName := GetRuntimeSourceName(DownloadUrl);
  DownloadPage.Clear;
  DownloadPage.Add(DownloadUrl, DotNetInstallerFileName, '');
  DownloadPage.SetText(
    '检测到未安装 .NET Desktop Runtime 8',
    '正在从 ' + CurrentDownloadSourceName + ' 下载...');
  DownloadPage.Show;

  try
    try
      DownloadPage.Download;
      Result := FileExists(InstallerPath);
    except
      Result := False;
    end;
  finally
    DownloadPage.Hide;
  end;
end;

function DownloadRuntimeInstaller(InstallerPath: string): Boolean;
begin
  Result :=
    TryDownloadRuntimeInstaller(DotNetDesktopRuntimePrimaryUrl, InstallerPath)
    or TryDownloadRuntimeInstaller(DotNetDesktopRuntimeSecondaryUrl, InstallerPath)
    or TryDownloadRuntimeInstaller(DotNetDesktopRuntimeMirrorUrl, InstallerPath);
end;

function InstallRuntimeInstaller(InstallerPath: string): Boolean;
var
  ResultCode: Integer;
begin
  InstallRuntimePage.SetText(
    '下载完成，正在安装 .NET Desktop Runtime 8',
    '安装器将自动静默安装运行环境，请稍候...');
  InstallRuntimePage.Show;
  InstallRuntimePage.Animate;

  Result := Exec(
    InstallerPath,
    '/install /quiet /norestart',
    '',
    SW_SHOW,
    ewWaitUntilTerminated,
    ResultCode);

  InstallRuntimePage.Hide;

  if Result then
  begin
    DotNetRequiresRestart := ResultCode = 3010;
    Result := (ResultCode = 0) or (ResultCode = 3010);
  end;
end;

function CanLaunchFastCliAfterInstall(): Boolean;
begin
  Result := not DotNetInstalledDuringSetup and not DotNetRequiresRestart;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  InstallerPath: string;
begin
  Result := '';

  if HasDesktopRuntime8() then
  begin
    exit;
  end;

  InstallerPath := ExpandConstant('{tmp}\' + DotNetInstallerFileName);

  WizardForm.StatusLabel.Caption := '检测到缺少 .NET Desktop Runtime 8，正在准备下载安装...';
  WizardForm.ProgressGauge.Style := npbstMarquee;

  if not DownloadRuntimeInstaller(InstallerPath) then
  begin
    Result := '下载 .NET Desktop Runtime 8 失败，所有预设下载地址均不可用，请检查网络或更换镜像地址后重试。';
    WizardForm.ProgressGauge.Style := npbstNormal;
    exit;
  end;

  WizardForm.StatusLabel.Caption := '下载完成，正在安装 .NET Desktop Runtime 8...';

  if not InstallRuntimeInstaller(InstallerPath) then
  begin
    Result := '安装 .NET Desktop Runtime 8 失败。';
    WizardForm.ProgressGauge.Style := npbstNormal;
    exit;
  end;

  WizardForm.ProgressGauge.Style := npbstNormal;
  DotNetInstalledDuringSetup := True;
  NeedsRestart := DotNetRequiresRestart;

  if not HasDesktopRuntime8() then
  begin
    Result := '安装器未检测到 .NET Desktop Runtime 8，请手动安装后重试。';
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssDone) and DotNetInstalledDuringSetup then
  begin
    if DotNetRequiresRestart then
    begin
      WizardForm.FinishedLabel.Caption :=
        'FastCli 已安装。'#13#10 +
        '.NET Desktop Runtime 已安装，系统提示需要重启后再启动 FastCli。';
    end
    else
    begin
      WizardForm.FinishedLabel.Caption :=
        'FastCli 已安装。'#13#10 +
        '.NET Desktop Runtime 已安装完成，请点击“完成”后手动启动 FastCli。';
    end;
  end;
end;
