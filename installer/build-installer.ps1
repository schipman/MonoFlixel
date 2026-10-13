# Publishes a self-contained win-x64 build (bundles the .NET runtime) and wraps it in Setup.exe.
param([string]$Version = "0.1.0")
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
  Remove-Item dist\publish -Recurse -Force -ErrorAction SilentlyContinue
  dotnet publish MonoFlixelStarter.csproj -c Release -r win-x64 --self-contained true -o dist\publish
  if ($LASTEXITCODE) { throw "publish failed" }
  $iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
  if (-not $iscc) { throw "Inno Setup not found (winget install JRSoftware.InnoSetup)" }
  & $iscc "/DAppVersion=$Version" installer\MonoFlixel.iss
  if ($LASTEXITCODE) { throw "installer failed" }
  Get-ChildItem dist\*.exe
} finally { Pop-Location }
