# Example reference topic

This is an example knowledge-base entry; replace it with real reference material for your domain - one
topic per file, organized under `kb/<subdir>/`. The first heading becomes the routing title and this
first prose line becomes the summary; the indexer extracts keywords from the body, deterministically.

## How chains use the knowledge base

A chain's `generate` node grounds on the most relevant entries with a `search` input binding, e.g.
`{ "search": "kb", "query": "{{ task }}", "k": 2, "as": "refs" }`, so the model drafts against real
reference text instead of recall. A node can also pin a fixed entry with a `ref` binding when a
constraint must always be present. The `/search kb <query>` command answers from these same entries.

## Discipline

Keep entries focused and factual; quote exact names, flags, and values. The deterministic oracle - a
compiler, a parser, the table validator - not this prose, is the final word on whether an artifact is
correct. Run `ratchet index kb` after adding or editing entries to refresh `kb/manifest.json`.
