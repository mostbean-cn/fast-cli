[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-OutputDirectory([string]$RepoRoot, [string]$Value)
{
    if ([string]::IsNullOrWhiteSpace($Value))
    {
        return (Join-Path $RepoRoot "artifacts\release")
    }

    if ([System.IO.Path]::IsPathRooted($Value))
    {
        return [System.IO.Path]::GetFullPath($Value)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $Value))
}

function Resolve-InnoSetupCompiler
{
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($null -ne $command)
    {
        return $command.Source
    }

    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($candidate in $candidates)
    {
        if (Test-Path $candidate)
        {
            return $candidate
        }
    }

    throw "ISCC.exe was not found. Please install Inno Setup 6 first."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "FastCli.Desktop\FastCli.Desktop.csproj"
$setupScriptPath = Join-Path $PSScriptRoot "FastCli.Setup.iss"
$packageExeScriptPath = Join-Path $PSScriptRoot "package-exe.ps1"
$OutputDir = Resolve-OutputDirectory -RepoRoot $repoRoot -Value $OutputDir

$projectXml = [xml](Get-Content -Path $projectPath -Raw -Encoding UTF8)
$version = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($version))
{
    throw "Failed to read <Version> from $projectPath."
}

$isccPath = Resolve-InnoSetupCompiler

& $packageExeScriptPath -Configuration $Configuration -Runtime $Runtime -OutputDir $OutputDir

$sourceExePath = Join-Path $OutputDir "FastCli.exe"
$setupExePath = Join-Path $OutputDir ("FastCli-Setup-v{0}.exe" -f $version)

Write-Host "Building setup package..."
Write-Host "Version: $version"
Write-Host "InnoSetup: $isccPath"
Write-Host "OutputDir: $OutputDir"

& $isccPath `
    "/DMyAppVersion=$version" `
    "/DMyAppSourceExe=$sourceExePath" `
    "/DMyOutputDir=$OutputDir" `
    $setupScriptPath

if ($LASTEXITCODE -ne 0)
{
    throw "Inno Setup compilation failed with exit code: $LASTEXITCODE"
}

if (-not (Test-Path $setupExePath))
{
    throw "Setup completed but output file was not found: $setupExePath"
}

Write-Host "SETUP created: $setupExePath"
