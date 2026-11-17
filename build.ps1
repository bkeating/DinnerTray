param(
    [switch]$Portable,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist')
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts/dotnet.ps1')
$sdk = Get-DinnerTraySdk
$project = Join-Path $PSScriptRoot 'DinnerTray.csproj'
$output = [IO.Path]::GetFullPath($OutputDirectory)
& $sdk publish $project -c Release -r win-x64 --self-contained $Portable.IsPresent.ToString().ToLowerInvariant() -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $output
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host "DinnerTray is ready: $output\DinnerTray.exe"
