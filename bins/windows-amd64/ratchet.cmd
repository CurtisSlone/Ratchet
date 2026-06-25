@echo off
rem Launch the LEGACY C# build (bins\csharp\ratchet.exe) in-memory to satisfy Smart App Control - a
rem .NET-only technique. The cross-platform Go build is a native exe you run directly
rem (bins\windows-amd64\ratchet.exe). Example: ratchet.cmd selftest
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\scripts\windows\run-cli.ps1" %*
