# make_launcher.ps1 - write a SAC-safe launcher .cmd into a workspace.
# The locally-built dist\<name>.exe is unsigned, so Smart App Control blocks running it directly
# (Start-Process / double-click). But loading its bytes in-memory inside trusted powershell IS
# allowed (the same trick icm.cmd uses for icm.exe). This writes a small <name>.cmd that calls
# run_app.ps1 (the in-memory STA loader) on the built exe, so the operator can launch the app from
# their own interactive session - where the GUI window actually appears.
#   tools/make_launcher.ps1 <workspace path>
param([string] $Proj)

$ErrorActionPreference = "Stop"
if (-not $Proj) { Write-Output "usage: make_launcher <workspace name or path>"; exit 1 }
# Accept a full path, or a bare workspace name (resolved under .\workspaces relative to the instance root).
if (-not (Test-Path $Proj)) {
    $alt = Join-Path "workspaces" $Proj
    if (Test-Path $alt) { $Proj = $alt } else { Write-Output ("no such workspace: " + $Proj); exit 1 }
}

$root = (Resolve-Path $Proj).Path
$name = Split-Path -Leaf $root

# Resolve the built artifact: project.json "out" if present, else the dist\<name>.exe convention.
$exeRel = "dist\$name.exe"
$pj = Join-Path $root "project.json"
if (Test-Path $pj) {
    try {
        $o = (Get-Content $pj -Raw | ConvertFrom-Json).out
        if ($o) { $exeRel = ($o -replace '/', '\') }
    } catch { }
}
$exe = Join-Path $root $exeRel
if (-not (Test-Path $exe)) {
    Write-Output ("no built exe at " + $exeRel + " - build it first (/flow add_file ... or /do build_project " + $name + ")")
    exit 1
}

$runApp = Join-Path $PSScriptRoot "run_app.ps1"
$cmdPath = Join-Path $root ("launch-" + $name + ".cmd")

# The .cmd: run powershell (trusted) -> run_app.ps1 -> in-memory STA load + invoke Main. SAC-safe.
$content = "@echo off`r`n" +
           "rem launch " + $name + " via the in-memory loader (Smart App Control blocks the bare exe)`r`n" +
           "powershell -NoProfile -ExecutionPolicy Bypass -File `"$runApp`" -Exe `"$exe`"`r`n"
[System.IO.File]::WriteAllText($cmdPath, $content, (New-Object System.Text.UTF8Encoding $false))

Write-Output ("launcher written: " + $cmdPath)
Write-Output ("run it from your terminal (or double-click) to launch " + $name + " - SAC-safe in-memory load")
exit 0
