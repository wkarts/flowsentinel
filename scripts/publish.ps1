[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$')]
    [string]$Version,
    [ValidateSet('win-x86', 'win-x64')]
    [string[]]$Runtime = @('win-x86', 'win-x64'),
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$versionFile = Join-Path $root 'eng\Version.props'

if (-not $Version) {
    [xml]$versionXml = Get-Content $versionFile
    $prefix = $versionXml.Project.PropertyGroup.VersionPrefix
    $suffix = $versionXml.Project.PropertyGroup.VersionSuffix
    $Version = if ([string]::IsNullOrWhiteSpace($suffix)) { $prefix } else { "$prefix-$suffix" }
}

$out = Join-Path $root "artifacts\release\$Version"
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item $out -ItemType Directory -Force | Out-Null

Push-Location $root
try {
    dotnet restore FlowSentinel.sln
    dotnet build FlowSentinel.sln -c Release --no-restore -p:Version=$Version
    if (-not $SkipTests) {
        dotnet test FlowSentinel.sln -c Release --no-build -p:Version=$Version
    }

    foreach ($rid in $Runtime) {
        $desktopDir = Join-Path $out "FlowSentinel-Desktop-$Version-$rid"
        $serviceDir = Join-Path $out "FlowSentinel-Service-$Version-$rid"

        dotnet publish src/FlowSentinel.Desktop/FlowSentinel.Desktop.csproj `
            -c Release -r $rid --self-contained true --no-restore `
            -p:Version=$Version -o $desktopDir

        dotnet publish src/FlowSentinel.Service/FlowSentinel.Service.csproj `
            -c Release -r $rid --self-contained true --no-restore `
            -p:Version=$Version -o $serviceDir

        Copy-Item scripts/install-service.ps1 $serviceDir
        Copy-Item scripts/uninstall-service.ps1 $serviceDir

        Compress-Archive -Path "$desktopDir\*" `
            -DestinationPath "$out\FlowSentinel-Desktop-$Version-$rid.zip" -CompressionLevel Optimal
        Compress-Archive -Path "$serviceDir\*" `
            -DestinationPath "$out\FlowSentinel-Service-$Version-$rid.zip" -CompressionLevel Optimal
    }

    $hashLines = Get-ChildItem $out -Filter *.zip | Sort-Object Name | ForEach-Object {
        $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $($_.Name)"
    }
    $hashLines | Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding utf8NoBOM
    Write-Host "Pacotes gerados em: $out" -ForegroundColor Green
} finally {
    Pop-Location
}
