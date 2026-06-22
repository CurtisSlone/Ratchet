# kb/

The single indexed-content bucket: reference, patterns, scaffolds, snippets, how-tos. Organize by
subdirectory (e.g. `kb/reference/`, `kb/patterns/`). Plain markdown - no `<!--icm-->` blocks needed.

Run `ratchet index kb` to (re)build `kb/manifest.json`, the routing index (id/title/summary/keywords,
extracted deterministically from content). Chains ground on this via `search`/`ref` input bindings
(e.g. `{ "search": "kb", "query": "{{ task }}", "k": 2, "as": "refs" }`), and `/search kb <query>`
answers from it.

Large or external libraries can be registered as additional knowledge bases in `ratchet.json`
(`knowledgeBases`), pointing anywhere on disk.
