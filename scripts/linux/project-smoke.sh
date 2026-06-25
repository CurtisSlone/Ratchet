#!/usr/bin/env bash
# project-smoke.sh - smoke test for the deterministic CLI surface (no model needed). The Linux analog
# of scripts/windows/project-smoke.ps1: that one exercised dotnet4-x's PowerShell/csc PROJECT tools,
# which have no cross-platform equivalent, so this exercises the engine's model-free verbs against the
# Go reference ratchet (selftest, open, index, validate-flow, flows, tools, list). Exit 0 = all pass.
#
# Usage: scripts/linux/project-smoke.sh [ratchet-dir]   (default: ../RatchetBox/Linux/go alongside this repo)
set -u

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
inst="${1:-$(cd "$repo/.." && pwd)/RatchetBox/Linux/go}"

# Find the ratchet binary: PATH first, then the repo's linux-amd64 build.
if command -v ratchet >/dev/null 2>&1; then
  RATCHET="ratchet"
elif [ -x "$repo/bins/linux-amd64/ratchet" ]; then
  RATCHET="$repo/bins/linux-amd64/ratchet"
else
  echo "ratchet binary not found (build it: make build, or put it on PATH)"; exit 1
fi

if [ ! -d "$inst" ]; then echo "ratchet dir not found: $inst"; exit 1; fi

pass=0; fail=0
check() { # check <label> <ok(0/1)> [detail]
  if [ "$2" -eq 0 ]; then echo "  ok    $1"; pass=$((pass+1));
  else echo "  FAIL  $1  ${3:-}"; fail=$((fail+1)); fi
}
# grep helper: 0 if pattern found in the given text
has() { printf '%s' "$1" | grep -qE "$2"; }

echo "project-smoke: $inst"

# 1. the deterministic core self test
o="$("$RATCHET" selftest 2>&1)"; rc=$?
check "selftest exits 0" "$rc"
has "$o" 'selftest: ALL PASS'; check "selftest ALL PASS" "$?" "$o"

# 2. open summarizes the ratchet and lists its declared tool
o="$("$RATCHET" open "$inst" 2>&1)"
has "$o" "ratchet 'go'"; check "open names the ratchet" "$?" "$o"
has "$o" 'go_build'; check "open lists the go_build tool" "$?" "$o"

# 3. index the KB (content -> manifest); deterministic, no model
o="$("$RATCHET" index "$inst/kb" 2>&1)"; rc=$?
check "index exits 0" "$rc"
has "$o" 'wrote [0-9]+ entries'; check "index wrote entries" "$?" "$o"

# 4. validate-flow lints the chain clean
o="$("$RATCHET" validate-flow "$inst" 2>&1)"; rc=$?
check "validate-flow exits 0" "$rc"
has "$o" '^ok    go'; check "validate-flow: go lints clean" "$?" "$o"

# 5. flows lists the go flow with its summary
o="$("$RATCHET" flows "$inst" 2>&1)"
has "$o" '(^|[^a-z])go([^a-z]|$)'; check "flows lists go" "$?" "$o"

# 6. tools lists the go_build oracle
o="$("$RATCHET" tools "$inst" 2>&1)"
has "$o" 'go_build'; check "tools lists go_build" "$?" "$o"

# 7. the index step wrote the KB library's routing manifest (kb/manifest.json)
[ -f "$inst/kb/manifest.json" ]; check "kb manifest.json written by index" "$?" "missing $inst/kb/manifest.json"

echo
if [ "$fail" -eq 0 ]; then echo "project-smoke: ALL PASS ($pass checks)"; exit 0; fi
echo "project-smoke: $fail FAILED, $pass passed"; exit 2
