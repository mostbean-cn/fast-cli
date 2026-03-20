@echo off
setlocal
where pwsh.exe >nul 2>nul
if %ERRORLEVEL%==0 (
  pwsh.exe -NoProfile -File "%~dp0package-exe.ps1" %*
) else (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0package-exe.ps1" %*
)
set EXIT_CODE=%ERRORLEVEL%
endlocal & exit /b %EXIT_CODE%
