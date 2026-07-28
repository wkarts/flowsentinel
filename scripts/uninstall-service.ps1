[CmdletBinding()]
param([string]$ServiceName = 'FlowSentinel')
$ErrorActionPreference = 'Stop'
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Execute este script como Administrador.'
}
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Write-Host 'Serviço removido.' -ForegroundColor Green
} else {
    Write-Host 'Serviço não encontrado.' -ForegroundColor Yellow
}
