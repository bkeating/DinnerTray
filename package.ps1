$ErrorActionPreference = 'Stop'
$artifacts = Join-Path $PSScriptRoot 'artifacts'
$publish = Join-Path $artifacts 'publish'
& (Join-Path $PSScriptRoot 'build.ps1') -Portable -OutputDirectory $publish
[xml]$project = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'DinnerTray.csproj')
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a three-part release version.' }
$archive = Join-Path $artifacts "DinnerTray-$version-windows-x64.zip"
Compress-Archive -LiteralPath (Join-Path $publish 'DinnerTray.exe') -DestinationPath $archive -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($archive))" | Set-Content -LiteralPath "$archive.sha256" -Encoding ascii
Write-Host "Release package: $archive"
