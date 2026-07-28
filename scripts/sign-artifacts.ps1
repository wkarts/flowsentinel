[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$Path,
    [Parameter(Mandatory)] [string]$CertificatePath,
    [Parameter(Mandatory)] [string]$CertificatePassword,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$signTool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
    Where-Object FullName -Match '\\x64\\signtool\.exe$' |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if (-not $signTool) { throw 'signtool.exe não foi localizado.' }

$files = if (Test-Path $Path -PathType Container) {
    Get-ChildItem $Path -Recurse -File | Where-Object Extension -In '.exe', '.dll'
} else {
    Get-Item $Path
}

foreach ($file in $files) {
    & $signTool.FullName sign /fd SHA256 /td SHA256 /tr $TimestampUrl `
        /f $CertificatePath /p $CertificatePassword $file.FullName
    if ($LASTEXITCODE -ne 0) { throw "Falha ao assinar $($file.FullName)." }
}
