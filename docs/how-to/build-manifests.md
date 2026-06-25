# Build the manifests (tools, flows, kb)

A ratchet has three manifest files. They are how the engine - and an agent crawling the directory -
discovers what the ratchet can do, without running anything. Two are generated; one you write by hand.

| Manifest | Build model | Build / refresh with | What it holds |
|---|---|---|---|
| `tools/manifest.json` | hand-authored | (edit it) | each tool: name, description, command, input schema |
| `flows/manifest.json` | generated | `tools/index_flows.ps1` | the flows index: id + summary + entry + node count |
| `kb/<lib>/manifest.json` | generated | `ratchet index <lib-dir>` | the kb routing index: title, summary, keywords per topic |

## tools/manifest.json (hand-authored)

Declare each tool: `name`, `description`, `command` (an argv array with `{arg}` placeholders),
`inputSchema`, an optional `stdin` field, and `timeout`. A bare `tools/*.ps1` with no entry is still
callable by name (zero-arg). Full contract: [Author tools](author-tools.md).

```json
{ "tools": [
  { "name": "csc_check", "description": "Compile a C# file with csc; OK or diagnostics.",
    "command": ["powershell","-NoProfile","-ExecutionPolicy","Bypass","-File","tools/csc_check.ps1"],
    "inputSchema": { "type":"object", "properties": { "code": { "type":"string" } }, "required":["code"] },
    "stdin": "code", "timeout": 60 }
] }
```

## flows/manifest.json (generated)

A quick-reference index of the ratchet's chains - `id`, `summary`, `entry`, node count - so an agent reads
ONE file instead of opening every `chain.json`. The engine itself discovers flows by scanning each
`flows/<chain>/chain.json`; this file is the fast index for humans and agents. Generate or refresh it
after adding, renaming, or re-summarizing a flow:

```
tools/index_flows.ps1          # run from the ratchet root -> writes flows/manifest.json
```

If a ratchet does not ship `index_flows.ps1`, copy it from `RatchetBox/Windows/dotnet4-x/tools/`.

## kb/<lib>/manifest.json (generated)

The routing index for one knowledge library - one entry per topic, each with the `title`, `summary`, and
`keywords` that retrieval matches against. Generate or refresh it with the engine's indexer, pointed at
the library directory (`kb` for a single-library ratchet, or `kb/<lib>` for one of several):

```
ratchet index <ratchet>\kb\<lib>      # -> writes kb/<lib>/manifest.json
```

How the kb works, the topic format, and building a new one: [Knowledge bases](knowledge-bases.md).

## Discovering a ratchet from its manifests

Point an agent at a ratchet directory and it learns the full surface from plain files, with no engine run:
`ratchet.json` (config) -> `flows/manifest.json` (what it does) -> `tools/manifest.json` (the tools) ->
`kb/<lib>/manifest.json` (the knowledge). See [Drive a ratchet](../agents/drive-a-ratchet.md).
