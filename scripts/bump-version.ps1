[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,
    [string]$Suffix = ''
)

$ErrorActionPreference = 'Stop'
$file = Join-Path (Split-Path -Parent $PSScriptRoot) 'eng\Version.props'
[xml]$xml = Get-Content $file
$xml.Project.PropertyGroup.VersionPrefix = $Version
$xml.Project.PropertyGroup.VersionSuffix = $Suffix
$settings = [System.Xml.XmlWriterSettings]::new()
$settings.Indent = $true
$settings.Encoding = [System.Text.UTF8Encoding]::new($false)
$writer = [System.Xml.XmlWriter]::Create($file, $settings)
try { $xml.Save($writer) } finally { $writer.Dispose() }
Write-Host "Versão definida como $Version$(if ($Suffix) { "-$Suffix" })"
