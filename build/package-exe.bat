@echo off
setlocal

set "REPO_ROOT=%~dp0.."
for %%I in ("%REPO_ROOT%") do set "REPO_ROOT=%%~fI"
set "PROJECT=%REPO_ROOT%\FastCli.Desktop\FastCli.Desktop.csproj"
set "OUTPUT_DIR=%REPO_ROOT%\artifacts\release"

for /f "tokens=3 delims=<>" %%V in ('findstr /R /C:"^[ ]*<Version>.*</Version>" "%PROJECT%"') do set "VERSION=%%V"

if not defined VERSION (
  echo Failed to read Version from %PROJECT%
  exit /b 1
)

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo Publishing self-contained exe...
echo Version: %VERSION%
echo OutputDir: %OUTPUT_DIR%

dotnet publish "%PROJECT%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "%OUTPUT_DIR%"

if errorlevel 1 (
  echo dotnet publish failed.
  exit /b 1
)

if not exist "%OUTPUT_DIR%\FastCli.exe" (
  echo Publish completed but FastCli.exe was not found.
  exit /b 1
)

echo EXE created: "%OUTPUT_DIR%\FastCli.exe"
exit /b 0
