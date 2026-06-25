# Build the legacy C# host (Windows)

Ratchet was originally a Windows-native C# host; the engine has since been rewritten in Go (the
cross-platform `ratchet` binary - see [Work on the host](work-on-the-host.md)). The original C# source
is preserved under `csharp_src/`. Both hosts implement the same engine and the same `ratchet.json` /
flow / tool / kb contract, so a ratchet runs under either.

**You do not need this to use Ratchet.** The committed Go binaries in [`bins/`](../../bins/) are the
current host, and on Windows the Go exe runs directly. Build the C# host only if you want either:

- **a Smart App Control-friendly Windows launch** - the C# build is a *managed .NET assembly*, so the
  in-memory PowerShell launcher can run it without tripping SAC (the native Go exe cannot use that
  trick - see [Windows execution](#windows-execution) below), or
- **the reference implementation** - to compare behavior against the Go port.

## What it is

- A single console exe built with the **in-box .NET Framework C# compiler** (`csc.exe`) - no SDK,
  NuGet, or MSBuild. The code targets **C# 5** (the pre-Roslyn in-box compiler): no string
  interpolation, `?.`, expression-bodied members, or tuples.
- The only non-default reference is `System.Web.Extensions.dll` (the JSON layer).
- Output: `bins/csharp/ratchet.exe` (a managed AnyCPU assembly).

## Build

From a Windows checkout (needs .NET Framework 4.x, already present on Windows):

```
powershell -ExecutionPolicy Bypass -File scripts\windows\build.ps1
```

`build.ps1` resolves the repo root from its own location, globs `csharp_src/` recursively, and writes
`bins\csharp\ratchet.exe`. It errors clearly if `csc.exe` or `csharp_src/` is missing.

## Windows execution

There are two Windows binaries, run two different ways:

| Build | Path | How to run |
| --- | --- | --- |
| **Go (current)** | `bins\windows-amd64\ratchet.exe` | native exe - run it directly: `bins\windows-amd64\ratchet.exe selftest` |
| **C# (legacy)** | `bins\csharp\ratchet.exe` | managed assembly - load in-memory via the launcher (SAC-safe) |

The C# exe is unsigned, so Smart App Control blocks running it directly. The launcher loads the
assembly's bytes in-memory inside the Microsoft-signed PowerShell, which SAC permits (this works only
because it is *managed* .NET; a native exe throws on `Assembly.Load`):

```
scripts\windows\run-cli.ps1 selftest                              # the deterministic core, model-free
scripts\windows\run-cli.ps1 ..\..\RatchetBox\Windows\dotnet4-x    # open the console
bins\windows-amd64\ratchet.cmd selftest                          # the .cmd just forwards to run-cli.ps1
```

If Smart App Control also blocks the *native* Go exe on your machine, this C# build + launcher is the
SAC-friendly path until the Go binary is code-signed.

## Verify

```
scripts\windows\run-cli.ps1 selftest                 # asserts the deterministic core
powershell -File scripts\windows\project-smoke.ps1   # the dotnet4-x project tools
powershell -File scripts\windows\mcp-smoke.ps1       # the MCP handshake + a csc_check call
```

## Differences from the Go host

- **stdin BOM.** The C# host prepended a UTF-8 BOM to a tool's stdin payload; the Windows ratchets'
  `.ps1` oracles strip a leading BOM for that reason. The Go host writes stdin as raw UTF-8 (no BOM) -
  the strip is harmless but no longer needed.
- **Build inputs.** C#: the in-box `csc` over `csharp_src/`. Go: `make build` over `go_src/` (pure Go,
  `CGO_ENABLED=0`), which is also what cross-compiles to every other platform.
- **Distribution.** The Go build ships prebuilt for every platform under `bins/<os>-<arch>/`; the C#
  build is Windows-only and built on demand into `bins/csharp/`.
