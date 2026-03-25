@echo off
setlocal

set "REPO_ROOT=%~dp0.."
for %%I in ("%REPO_ROOT%") do set "REPO_ROOT=%%~fI"
set "PROJECT=%REPO_ROOT%\FastCli.Desktop\FastCli.Desktop.csproj"
set "SETUP_SCRIPT=%~dp0FastCli.Setup.iss"
set "OUTPUT_ROOT=%REPO_ROOT%\artifacts\release-setup"
set "MIRROR_URL=https://github.com/mostbean-cn/fast-cli/releases/download/runtime-cache/windowsdesktop-runtime-8.0.25-win-x64.exe"

for /f "tokens=3 delims=<>" %%V in ('findstr /R /C:"^[ ]*<Version>.*</Version>" "%PROJECT%"') do set "VERSION=%%V"

if not defined VERSION (
  echo Failed to read Version from %PROJECT%
  exit /b 1
)

set "APP_DIR=%OUTPUT_ROOT%\app\FastCli-v%VERSION%-win-x64"
set "SETUP_EXE=%OUTPUT_ROOT%\FastCli-Setup-v%VERSION%.exe"
set "ISCC_PATH="
for /f "delims=" %%I in ('where ISCC.exe 2^>nul') do if not defined ISCC_PATH set "ISCC_PATH=%%I"
if not defined ISCC_PATH if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "ISCC_PATH=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not defined ISCC_PATH if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC_PATH=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC_PATH if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC_PATH=%ProgramFiles%\Inno Setup 6\ISCC.exe"

if not defined ISCC_PATH (
  echo ISCC.exe was not found. Please install Inno Setup 6 first.
  exit /b 1
)

if not exist "%OUTPUT_ROOT%" mkdir "%OUTPUT_ROOT%"

call :clean_dir "%APP_DIR%" || exit /b 1

echo Publishing framework-dependent app...
echo Version: %VERSION%
echo StageDir: %APP_DIR%

dotnet publish "%PROJECT%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained false ^
  -p:PublishSingleFile=false ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "%APP_DIR%"

if errorlevel 1 (
  echo dotnet publish failed.
  exit /b 1
)

if not exist "%APP_DIR%\FastCli.exe" (
  echo Publish completed but FastCli.exe was not found.
  exit /b 1
)
if not exist "%APP_DIR%\Microsoft.Terminal.Control.dll" (
  echo Publish completed but Microsoft.Terminal.Control.dll was not found.
  exit /b 1
)
if not exist "%APP_DIR%\conpty.dll" (
  echo Publish completed but conpty.dll was not found.
  exit /b 1
)
if not exist "%APP_DIR%\OpenConsole.exe" (
  echo Publish completed but OpenConsole.exe was not found.
  exit /b 1
)

if exist "%SETUP_EXE%" del /f /q "%SETUP_EXE%"
if exist "%SETUP_EXE%" (
  echo Failed to remove old setup package: "%SETUP_EXE%"
  exit /b 1
)

echo Building setup package...
echo InnoSetup: %ISCC_PATH%
echo StageDir: %APP_DIR%
echo OutputDir: %OUTPUT_ROOT%

"%ISCC_PATH%" ^
  /DMyAppVersion=%VERSION% ^
  /DMyAppSourceDir=%APP_DIR% ^
  /DMyOutputDir=%OUTPUT_ROOT% ^
  /DMyDotNetRuntimeMirrorUrl=%MIRROR_URL% ^
  "%SETUP_SCRIPT%"

if errorlevel 1 (
  echo Inno Setup compilation failed.
  exit /b 1
)

if not exist "%SETUP_EXE%" (
  echo Setup completed but output file was not found.
  exit /b 1
)

echo SETUP created: "%SETUP_EXE%"
exit /b 0

:clean_dir
set "TARGET_DIR=%~1"
if not exist "%TARGET_DIR%" (
  exit /b 0
)

rmdir /s /q "%TARGET_DIR%" >nul 2>nul
if exist "%TARGET_DIR%" (
  echo Failed to clean directory: "%TARGET_DIR%"
  exit /b 1
)
exit /b 0
