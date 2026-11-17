$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts/dotnet.ps1')
$sdk = Get-DinnerTraySdk
if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw 'Install Node.js 22+ to run the JavaScript tests.' }
Push-Location $PSScriptRoot
try {
    & $sdk run --project tests/RecoveryTests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Recovery/physics tests failed.' }
    & $sdk run --project tests/DesktopGameChecks/DesktopGameChecks.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Desktop game checks failed.' }
    node --test tests/health-script.test.mjs tests/video-frame.test.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Browser-script tests failed.' }
} finally { Pop-Location }
