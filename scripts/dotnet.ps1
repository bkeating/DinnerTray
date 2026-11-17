function Get-DinnerTraySdk {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) {
        $sdks = & $command.Source --list-sdks
        if ($LASTEXITCODE -eq 0 -and ($sdks -match '^10\.')) { return $command.Source }
    }
    $localSdk = Join-Path $env:LOCALAPPDATA 'DinnerTray\dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $localSdk) { return $localSdk }
    throw 'Install the .NET 10 SDK for Windows, then run this script again.'
}
