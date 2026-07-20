$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
  $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
$mem = Join-Path (Split-Path $PSScriptRoot -Parent) "shared\translation-memory.json"
$out = Join-Path $PSScriptRoot "ClaudeZhPatch.exe"

if (-not (Test-Path $csc)) { throw "csc.exe not found (need .NET Framework 4.x)" }
if (-not (Test-Path $mem)) { throw "missing shared/translation-memory.json" }
if (-not (Test-Path ".\Program.cs")) { throw "Program.cs missing" }
if (-not (Test-Path ".\app.manifest")) { throw "app.manifest missing" }

if (Test-Path $out) { Remove-Item $out -Force }

$compileArgs = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/platform:anycpu",
    "/win32manifest:app.manifest",
    "/r:System.Windows.Forms.dll",
    "/r:System.Drawing.dll",
    "/r:System.Web.Extensions.dll",
    "/resource:$mem,translation-memory.json",
    "/out:$out",
    "Program.cs"
)

Write-Host "Compiling ClaudeZhPatch.exe ..."
& $csc @compileArgs
if ($LASTEXITCODE -ne 0) { throw "csc failed: $LASTEXITCODE" }

$fi = Get-Item $out
Write-Host ("OK size={0} time={1}" -f $fi.Length, $fi.LastWriteTime)
