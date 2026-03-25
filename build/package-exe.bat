@echo off
setlocal

set "REPO_ROOT=%~dp0.."
for %%I in ("%REPO_ROOT%") do set "REPO_ROOT=%%~fI"
set "PROJECT=%REPO_ROOT%\FastCli.Desktop\FastCli.Desktop.csproj"
set "OUTPUT_ROOT=%REPO_ROOT%\artifacts\release"

for /f "tokens=3 delims=<>" %%V in ('findstr /R /C:"^[ ]*<Version>.*</Version>" "%PROJECT%"') do set "VERSION=%%V"

if not defined VERSION (
  echo Failed to read Version from %PROJECT%
  exit /b 1
)

set "PACKAGE_NAME=FastCli-v%VERSION%-win-x64-portable"
set "PUBLISH_DIR=%OUTPUT_ROOT%\_publish\%PACKAGE_NAME%"
set "PACKAGE_DIR=%OUTPUT_ROOT%\%PACKAGE_NAME%"
set "PACKAGE_ZIP=%OUTPUT_ROOT%\%PACKAGE_NAME%.zip"

if not exist "%OUTPUT_ROOT%" mkdir "%OUTPUT_ROOT%"

call :clean_dir "%PUBLISH_DIR%" || exit /b 1
call :clean_dir "%PACKAGE_DIR%" || exit /b 1

if exist "%PACKAGE_ZIP%" (
  del /f /q "%PACKAGE_ZIP%" >nul 2>nul
  if exist "%PACKAGE_ZIP%" (
    echo Failed to remove old package: "%PACKAGE_ZIP%"
    exit /b 1
  )
)

echo Publishing self-contained portable app...
echo Version: %VERSION%
echo PublishDir: %PUBLISH_DIR%

dotnet publish "%PROJECT%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=false ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "%PUBLISH_DIR%"

if errorlevel 1 (
  echo dotnet publish failed.
  exit /b 1
)

if not exist "%PUBLISH_DIR%\FastCli.exe" (
  echo Publish completed but FastCli.exe was not found.
  exit /b 1
)
if not exist "%PUBLISH_DIR%\Microsoft.Terminal.Control.dll" (
  echo Publish completed but Microsoft.Terminal.Control.dll was not found.
  exit /b 1
)
if not exist "%PUBLISH_DIR%\conpty.dll" (
  echo Publish completed but conpty.dll was not found.
  exit /b 1
)
if not exist "%PUBLISH_DIR%\OpenConsole.exe" (
  echo Publish completed but OpenConsole.exe was not found.
  exit /b 1
)

del /f /q "%PUBLISH_DIR%\*.pdb" >nul 2>nul

mkdir "%PACKAGE_DIR%" >nul 2>nul
if errorlevel 1 (
  echo Failed to create package directory: "%PACKAGE_DIR%"
  exit /b 1
)

echo Copying portable package files...
xcopy "%PUBLISH_DIR%\*" "%PACKAGE_DIR%\" /E /I /Y >nul
if errorlevel 1 (
  echo Failed to copy publish output into portable package directory.
  exit /b 1
)

echo Creating zip package...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%PACKAGE_DIR%' -DestinationPath '%PACKAGE_ZIP%' -Force"
if errorlevel 1 (
  echo Failed to create zip package.
  exit /b 1
)

echo Portable directory: "%PACKAGE_DIR%"
echo Portable zip: "%PACKAGE_ZIP%"
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
