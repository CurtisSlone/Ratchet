# workspaces/

Project workspaces - one subdirectory per project (this replaces the old `out/`). The **active**
workspace is the session focus: `/ws create <name>` makes one and selects it, `/ws switch <name>`
selects an existing one. The active workspace is injected into prompts and seeded into chains as
`$workspace`, so workspace-bound chains (e.g. add_file / edit_file) target it as the project.

Writes (generated files, chain run-state under `runs/`, the search cache under `.index/`) stay under
the instance's workdir. Build outputs are not indexed.
