[CmdletBinding()]
param(
    [switch]$SkipRestore
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not $SkipRestore) {
        dotnet restore FlowSentinel.sln --locked-mode:$false
    }

    dotnet format analyzers FlowSentinel.sln --verify-no-changes --no-restore --severity error
    dotnet build FlowSentinel.sln -c Release --no-restore
    dotnet test FlowSentinel.sln -c Release --no-build --collect:"XPlat Code Coverage" `
        --results-directory "$root\artifacts\test-results"
} finally {
    Pop-Location
}
