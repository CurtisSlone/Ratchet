# linux dev/smoke scripts (POSIX bash)

Model-free smoke tests for the Linux/WSL build. Both default to the Go reference ratchet at
`../RatchetBox/go` (alongside this repo); pass a ratchet dir as `$1` to override. They find the
`ratchet` binary on PATH, else fall back to `bins/linux-amd64/ratchet`.

- `project-smoke.sh` — exercises the deterministic CLI verbs (selftest, open, index, validate-flow,
  flows, tools) and asserts their output. The Linux analog of `windows/project-smoke.ps1` (which drove
  dotnet4-x's PowerShell/csc project tools, with no cross-platform equivalent). Needs only the binary.
- `mcp-smoke.sh` — drives the MCP server over stdio (initialize / tools/list / a real `go_build` tool
  call, valid + invalid / ping / unknown-tool error) and asserts the JSON-RPC responses. The analog of
  `windows/mcp-smoke.ps1` (csc_check -> go_build). Needs `go` on PATH (the oracle runs `go build`) and
  `python3` (to parse the responses).

Run both via the repo-root Makefile: `make smoke`.
