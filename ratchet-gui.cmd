@echo off
rem ratchet-gui (legacy) - open the GUI on a folder (default: current dir). PATH-friendly: add this
rem folder to PATH and run "ratchet-gui ." from any directory. Runs in-memory to satisfy Smart App Control.
powershell -STA -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-gui.ps1" -Folder "%~1"
