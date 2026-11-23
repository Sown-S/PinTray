# Builds dist\PinTray.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
#   .\build.ps1          build
#   .\build.ps1 -Test    build, then compile and run the tests
param([switch]$Test)
$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "C# compiler not found: $csc" }
$root = $PSScriptRoot
$src = @(Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.cs | ForEach-Object FullName)
$refs = '/r:System.Management.dll', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null

& $csc /nologo /optimize+ /target:winexe /platform:anycpu /codepage:65001 /out:"$dist\PinTray.exe" @refs @src
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host "Built $dist\PinTray.exe"

if ($Test) {
    $tests = @(Get-ChildItem (Join-Path $root 'tests') -Filter *.cs | ForEach-Object FullName)
    $obj = Join-Path $root 'obj'
    New-Item -ItemType Directory -Force $obj | Out-Null
    & $csc /nologo /target:exe /platform:anycpu /codepage:65001 /main:PinTray.Tests.TestMain /out:"$obj\PinTray.Tests.exe" @refs @src @tests
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
    & "$obj\PinTray.Tests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}
