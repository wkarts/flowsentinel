[CmdletBinding()]
param(
    [string]$BinaryPath = (Join-Path $PSScriptRoot 'FlowSentinel.Service.exe'),
    [string]$ServiceName = 'FlowSentinel',
    [string]$DataRoot = "$env:ProgramData\FlowSentinel"
)

$ErrorActionPreference = 'Stop'
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Execute este script como Administrador.'
}

$BinaryPath = (Resolve-Path $BinaryPath).Path
$DataRoot = [Environment]::ExpandEnvironmentVariables($DataRoot)
New-Item $DataRoot -ItemType Directory -Force | Out-Null

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

sc.exe create $ServiceName binPath= "`"$BinaryPath`"" start= auto DisplayName= "FlowSentinel Monitor" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Não foi possível criar o serviço.' }
sc.exe description $ServiceName "Motor de monitoramento, regras e notificações FlowSentinel." | Out-Null
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/15000/restart/60000 | Out-Null

$serviceRegistryPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
New-ItemProperty -Path $serviceRegistryPath -Name Environment -PropertyType MultiString `
    -Value @("FLOWSENTINEL_DATA_ROOT=$DataRoot") -Force | Out-Null

Start-Service $ServiceName
Get-Service $ServiceName
Write-Host "Dados do serviço: $DataRoot" -ForegroundColor Green
