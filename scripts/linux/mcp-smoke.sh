#!/usr/bin/env bash
# mcp-smoke.sh - drive the Ratchet MCP server over stdio exactly as a client would, and assert the
# JSON-RPC responses. Model-free (initialize / tools/list / a real tool call / ping / error), so it
# runs without Ollama. The Linux analog of scripts/windows/mcp-smoke.ps1: it exercises the Go ratchet's
# go_build oracle (valid + invalid Go) instead of dotnet4-x's csc_check. Needs `go` on PATH (the oracle
# runs `go build`) and python3 (to parse the responses). Exit 0 = all pass.
#
# Usage: scripts/linux/mcp-smoke.sh [ratchet-dir]   (default: ../RatchetBox/Linux/go alongside this repo)
set -u

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
inst="${1:-$(cd "$repo/.." && pwd)/RatchetBox/Linux/go}"

if command -v ratchet >/dev/null 2>&1; then RATCHET="ratchet"
elif [ -x "$repo/bins/linux-amd64/ratchet" ]; then RATCHET="$repo/bins/linux-amd64/ratchet"
else echo "ratchet binary not found (build it: make build, or put it on PATH)"; exit 1; fi

# the go_build oracle shells out to `go`; make sure it is reachable.
if ! command -v go >/dev/null 2>&1; then export PATH="$PATH:/usr/local/go/bin"; fi
if ! command -v go >/dev/null 2>&1; then echo "go not found on PATH (the go_build oracle needs it)"; exit 1; fi
if ! command -v python3 >/dev/null 2>&1; then echo "python3 not found (needed to parse JSON-RPC)"; exit 1; fi
if [ ! -d "$inst" ]; then echo "ratchet dir not found: $inst"; exit 1; fi

# A real client's opening handshake, then a few tool calls. One JSON object per line. We exercise a
# declared instance tool (go_build, the Go type-check oracle) plus ping and an unknown-tool error.
msgs=(
  '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"mcp-smoke","version":"1.0"}}}'
  '{"jsonrpc":"2.0","method":"notifications/initialized"}'
  '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
  '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"go_build","arguments":{"code":"package solution\n\nfunc Add(a, b int) int { return a + b }\n"}}}'
  '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"go_build","arguments":{"code":"package solution\n\nfunc Bad( {\n}\n"}}}'
  '{"jsonrpc":"2.0","id":5,"method":"ping"}'
  '{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"does_not_exist","arguments":{}}}'
)

# Capture the server's stdout to a temp file. We pass that path to python3 via an env var so the
# heredoc can be python3's program (stdin) without colliding with the JSON-RPC data.
out="$(mktemp)"
trap 'rm -f "$out"' EXIT
printf '%s\n' "${msgs[@]}" | "$RATCHET" mcp "$inst" 2>/dev/null > "$out"

MCP_OUT="$out" python3 <<'PY'
import os, sys, json
resp = {}
for line in open(os.environ["MCP_OUT"]).read().splitlines():
    line = line.strip()
    if not line:
        continue
    try:
        o = json.loads(line)
    except Exception:
        continue
    if isinstance(o, dict) and o.get("id") is not None:
        resp[o["id"]] = o

fails = 0
def check(name, cond):
    global fails
    if cond:
        print("  ok    " + name)
    else:
        print("  FAIL  " + name)
        fails += 1

def res(i):
    return (resp.get(i) or {}).get("result") or {}

# 1: initialize echoes the client's protocol version + advertises tools capability + serverInfo
init = res(1)
check("initialize: protocolVersion echoed", init.get("protocolVersion") == "2025-06-18")
check("initialize: tools capability",       (init.get("capabilities") or {}).get("tools") is not None)
check("initialize: serverInfo.name",        bool((init.get("serverInfo") or {}).get("name")))

# 2: tools/list advertises the instance's declared tools (go_build among them)
names = [t.get("name") for t in (res(2).get("tools") or [])]
check("tools/list: go_build advertised", "go_build" in names)
check("tools/list: at least one tool",   len(names) >= 1)

# 3: a valid compilation unit passes the oracle (content, not an error)
ok = res(3)
content = ok.get("content") or [{}]
check("go_build (valid): returns content", len(content[0].get("text", "")) > 0)
check("go_build (valid): not an error",    ok.get("isError") is False)

# 4: an invalid unit fails the oracle (isError set, diagnostics returned)
check("go_build (invalid): flagged as error", res(4).get("isError") is True)

# 5: ping ok
check("ping: ok", resp.get(5, {}).get("result") is not None)

# 6: unknown tool -> JSON-RPC error (-32602)
err = (resp.get(6) or {}).get("error") or {}
check("unknown tool: error -32602", err.get("code") == -32602)

print()
if fails == 0:
    print("mcp-smoke: ALL PASS")
    sys.exit(0)
print("mcp-smoke: %d FAILED" % fails)
sys.exit(1)
PY
