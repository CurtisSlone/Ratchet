# run-cli.ps1 - launch the LEGACY C# build without tripping Smart App Control. An unsigned local exe is
# blocked from running directly, but loading a managed assembly's bytes in-memory inside the
# Microsoft-signed PowerShell is allowed.
#
# IMPORTANT: this technique is .NET-only. It works for the C# build (a managed assembly under
# bins\csharp\), NOT for the cross-platform Go build - that is a NATIVE exe you run directly
# (bins\windows-<arch>\ratchet.exe). See docs\how-to\build-csharp-host.md.
#
# Location-independent: it resolves the repo root from this script's own path ($PSScriptRoot is
# <repo>\scripts\windows), so it works from the Ratchet base dir or anywhere.
#   Example:  powershell -ExecutionPolicy Bypass -File scripts\windows\run-cli.ps1 selftest

param([Parameter(ValueFromRemainingArguments = $true)][string[]] $CliArgs)

# Convert-Path (not Resolve-Path) so the result is a plain filesystem path, not a provider-qualified
# one (Microsoft.PowerShell.Core\FileSystem::...), which [System.IO.File] cannot parse - e.g. over a
# \\wsl.localhost\ UNC mount.
$repoRoot = Convert-Path (Join-Path $PSScriptRoot "..\..")

# The C# build is a single AnyCPU managed assembly at bins\csharp\ratchet.exe (built by build.ps1).
$exe = Join-Path $repoRoot "bins\csharp\ratchet.exe"
if (-not (Test-Path $exe)) {
    Write-Error "C# build not found at $exe - build it first: powershell -ExecutionPolicy Bypass -File scripts\windows\build.ps1"
    exit 1
}
$exe = Convert-Path -LiteralPath $exe

if ($null -eq $CliArgs) { $CliArgs = @() }
$bytes = [System.IO.File]::ReadAllBytes($exe)
$asm = [System.Reflection.Assembly]::Load($bytes)   # managed assembly only - native PEs throw here
$rv = $asm.EntryPoint.Invoke($null, @(, [string[]]$CliArgs))
exit [int]$rv
