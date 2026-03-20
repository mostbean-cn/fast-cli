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

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "FastCli.Desktop\FastCli.Desktop.csproj"
$OutputDir = Resolve-OutputDirectory -RepoRoot $repoRoot -Value $OutputDir

$projectXml = [xml](Get-Content -Path $projectPath -Raw -Encoding UTF8)
$version = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($version))
{
    throw "Failed to read <Version> from $projectPath."
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

Write-Host "Publishing single-file exe..."
Write-Host "Version: $version"
Write-Host "OutputDir: $OutputDir"

& dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $OutputDir

if ($LASTEXITCODE -ne 0)
{
    throw "dotnet publish failed with exit code: $LASTEXITCODE"
}

$exePath = Join-Path $OutputDir "FastCli.exe"

if (-not (Test-Path $exePath))
{
    throw "Publish completed but output file was not found: $exePath"
}

Write-Host "EXE created: $exePath"
