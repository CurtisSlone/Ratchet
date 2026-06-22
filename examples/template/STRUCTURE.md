# Instance structure (the four-bucket model)

An instance is **one config that references directories**. `ratchet <dir>` (or `ratchet <ratchet.json>`) composes
and runs it. `workdir` (default: this folder) is the write/sandbox root; everything else defaults
under it or is overridden by a dir field in `ratchet.json`.

```
ratchet.json            the launch config (identity, models, dir references, knowledgeBases)
SYSTEM.md           operating rules woven into prompts

kb/                 INDEXED CONTENT - the only knowledge bucket
  <subdir>/*.md       reference, patterns, scaffolds, snippets, how-tos (organize by subdir)
  manifest.json       GENERATED routing index (run: ratchet index kb). Do not hand-edit.

flows/              ACTION CHAINS
  <chain>/
    chain.json        { id, summary, entry, inputs?, budgets, nodes[] }
    actions/<node>/
      action.json     { id, kind: action|generate|ai_branch|summarizer|exit, inputs[], edges }
      prompt.md       the model's instructions ({{ slot }} templating) - for generate/ai_branch

tools/              SCRIPTS (run by name: tools/<name>.ps1)
  manifest.json       AUTHORED tool declarations (command/inputSchema/stdin/timeout) - optional;
                      a bare script with no entry is still callable by name (zero-arg)

schemas/            TSV oracle: <table>.json column schemas
samples/            TSV oracle: <table>.txt reference rows (cross-table refs)

workspaces/         project workspaces (one subdir per project; the active one is the session focus)
.index/  runs/      generated at runtime (search cache; chain run-state) - gitignore
```

## The model in one breath

- **Config = wiring; each directory self-describes via a manifest.** `kb/` is searched and injected
  into chains; `flows/` are the executable graphs; `tools/` are the scripts the chains run;
  `schemas/`+`samples/` are the data oracle.
- A chain node sees ONLY its declared `inputs[]` - bound from a prior node, the `$input`/`$workspace`
  run seeds, a chain-declared input slot, a fixed kb `ref`, or a kb `search`.
- The model proposes (a generated artifact, or a gated edge); a tool/oracle decides; the operator drives.

## Authoring a new instance

1. Copy this folder; set `name`/`domain`/`models` in `ratchet.json`.
2. Drop reference docs into `kb/<subdir>/`, then `ratchet index kb` to build the routing index.
3. Declare tools in `tools/manifest.json` (and drop their scripts in `tools/`).
4. Author chains under `flows/<chain>/`; lint with `ratchet validate-flow <dir>`.
5. Open it: `ratchet <dir>`.
