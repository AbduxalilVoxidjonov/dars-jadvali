<#
.SYNOPSIS
    Localhost test serverini ishga tushiradi (brauzerda sinash uchun).

.DESCRIPTION
    DarsJadvali.Web loyihasini ishga tushiradi. Bu WPF dasturining brauzerdagi
    "sinov maydoni" - biznes-mantiq (Application/Infrastructure) bir xil.
    Windows, macOS va Linux'da ishlaydi.

.EXAMPLE
    .\run-web.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$webProject  = Join-Path $projectRoot "src/DarsJadvali.Web"

if (-not (Test-Path $webProject)) {
    throw "Web loyihasi topilmadi: $webProject"
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnet) {
    throw ".NET SDK topilmadi. https://dotnet.microsoft.com/download/dotnet/8.0 dan .NET 8 SDK ni o'rnating."
}

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " Dars Jadvali - localhost test serveri" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host " Manzil: " -NoNewline
Write-Host "http://localhost:5080" -ForegroundColor Green
Write-Host " Brauzerda shu manzilni oching."
Write-Host " To'xtatish uchun: Ctrl + C"
Write-Host ""

& dotnet run --project $webProject

if ($LASTEXITCODE -ne 0) {
    throw "Server ishga tushmadi (kod: $LASTEXITCODE)."
}
