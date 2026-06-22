# Instance template

A reusable, domain-agnostic skeleton for a new Ratchet instance, on the current standard: one
`ratchet.json` config plus four buckets (`kb/` indexed content, `flows/` action chains, `tools/`
scripts, `schemas/`+`samples/` the TSV oracle). The host (`ratchet`) opens any instance directory.
Everything here is a minimal, WORKING example you replace.

## Start a new instance

1. Copy this folder to a new instance dir, e.g. `xcopy /E /I examples\template examples\my-instance`
   (or copy in your file manager).
2. Edit `ratchet.json`: set `name` and `domain`, and point `models` at models you have in Ollama.
3. Replace the example content:
   - drop reference docs into `kb/<subdir>/`, then `ratchet index kb` to (re)build the routing index;
   - declare your tools in `tools/manifest.json` and drop their scripts in `tools/`;
   - author chains under `flows/<chain>/` (copy the `draft` example), lint with `ratchet validate-flow .`;
   - define oracle `schemas/<table>.json` + `samples/<table>.txt` if your domain has data tables.
4. Open it: `ratchet <my-instance>` (or `ratchet <my-instance>\ratchet.json`).
5. Delete the placeholders once you have your own: `flows/draft/`, `tools/example_check.ps1`,
   `kb/reference/example-topic.md`, `schemas/example.*`, `samples/example.*`.

## What's in here (all replaceable examples)

- `flows/draft/` - an example action chain: generate (grounded in kb) -> check (oracle) -> repair
  once -> exit. The canonical generate-verify-repair shape.
- `tools/example_check.ps1` + `tools/manifest.json` - the example oracle the chain calls.
- `kb/reference/example-topic.md` + `kb/manifest.json` - an example indexed entry.
- `schemas/example.json` + `samples/example.txt` - an example TSV oracle table.

## What goes where

See `STRUCTURE.md` for the full layout and `flows/README.md` for chain anatomy. Each folder has a
short `README.md` (skipped by the indexer, so they never pollute routing).

The host is unchanged across instances - a new domain (a different language, a game, a dataset) is
just a new instance copied from this skeleton. Verify a fresh copy with `ratchet open <my-instance>` and
`ratchet validate-flow <my-instance>`.
