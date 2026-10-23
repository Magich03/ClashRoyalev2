param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
# Each application has its own portable output; never publish the solution to one folder.
$projects = @{
    server = 'ClashRoyale.Server/ClashRoyale.Server.csproj'
    patcher = 'ClashRoyale.Patcher/ClashRoyale.Patcher.csproj'
    downloader = 'Downloader/Downloader.csproj'
}
foreach ($name in @('server','patcher','downloader')) {
    $output = Join-Path $PSScriptRoot "artifacts/$Configuration/$name"
    & dotnet publish (Join-Path $PSScriptRoot $projects[$name]) -c $Configuration --disable-build-servers -o $output
    if ($LASTEXITCODE -ne 0) { throw "Publishing $name failed ($LASTEXITCODE)" }
}
Write-Host "Ready: $PSScriptRoot/artifacts/$Configuration"
