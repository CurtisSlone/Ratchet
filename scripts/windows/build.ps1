# build.ps1 - compile the ORIGINAL C# host with the C# compiler that ships with Windows. No SDK, no
# NuGet, no MSBuild: we call the in-box .NET Framework csc.exe directly. Produces one exe:
#   bins\csharp\ratchet.exe   console CLI / operator console   (csharp_src\Cli\ has its Main; -target:exe)
#
# This is the legacy build, kept for reference now that the engine has been rewritten in Go. The C#
# sources live under csharp_src\ (renamed from the original src\); the Go build is separate (Makefile).
#
# -noconfig ignores the machine csc.rsp so the build is deterministic; -langversion:5 pins the language
# to what this in-box (pre-Roslyn) compiler supports. Non-default reference: System.Web.Extensions.dll
# (JavaScriptSerializer, the JSON layer).
#
# Run from anywhere (it resolves the repo root from its own location):
#   powershell -ExecutionPolicy Bypass -File scripts\windows\build.ps1

$ErrorActionPreference = "Stop"
# This script lives at <repo>\scripts\windows\build.ps1; the repo root is two levels up.
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Split-Path -Parent (Split-Path -Parent $scriptDir)

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "csc.exe (.NET Framework C# compiler) not found. Is .NET Framework 4.x installed?" }

$srcDir = Join-Path $repo "csharp_src"
if (-not (Test-Path $srcDir)) { throw "C# sources not found at $srcDir (expected the renamed src -> csharp_src)" }
$src = Get-ChildItem $srcDir -Recurse -Filter *.cs | ForEach-Object { $_.FullName }

$outDir = Join-Path $repo "bins\csharp"
New-Item -ItemType Directory -Force $outDir | Out-Null
$outExe = Join-Path $outDir "ratchet.exe"

& $csc -nologo -noconfig -optimize+ -langversion:5 -warn:4 -target:exe -platform:anycpu `
    "-reference:System.dll" "-reference:System.Core.dll" "-reference:System.Web.Extensions.dll" `
    "-out:$outExe" $src
if ($LASTEXITCODE -ne 0) { throw "build failed (csc exit $LASTEXITCODE)" }
Write-Host "built $outExe"
