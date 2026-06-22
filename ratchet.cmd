@echo off
rem ratchet - the console CLI (open/chat/mcp/validate/gen). PATH-friendly. Runs in-memory to satisfy
rem Smart App Control. Example: ratchet validate examples\dotnet skills
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-cli.ps1" %*
