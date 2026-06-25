# scripts/ — local dev support, by platform

Dev and smoke scripts grouped by platform. Each platform dir holds the scripts that run there.

- `windows/` — PowerShell smokes/helpers from the original host (`project-smoke.ps1`,
  `mcp-smoke.ps1`, `run-cli.ps1`). They target the legacy `ratchet.exe`; they will be repointed
  at the Go binary (`bins/windows-amd64/ratchet.exe`) as the port lands.
- `linux/`, `darwin/` — POSIX equivalents, added as the engine is ported.

Build is driven from the repo-root `Makefile` (`make build`, `make cross`, `make test`).
