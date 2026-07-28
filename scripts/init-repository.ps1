[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$RemoteUrl,
    [string]$DefaultBranch = 'main'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not (Test-Path '.git')) { git init }
    git checkout -B $DefaultBranch
    git add .
    git commit -m 'feat: estrutura inicial do FlowSentinel'
    if (git remote get-url origin 2>$null) { git remote set-url origin $RemoteUrl }
    else { git remote add origin $RemoteUrl }
    git push -u origin $DefaultBranch
} finally { Pop-Location }
