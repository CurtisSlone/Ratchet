# tools/
Scripts the host runs. Declare each in `manifest.json` (a `command` argv or a `script`, plus an
optional `inputSchema`/`stdin`/`timeout`/`env`); a bare `*.ps1` with no entry is still callable by
name (zero-arg). The model only fills declared arguments; the command itself is authored here, never
by the model. See `example_check.ps1` + `manifest.json` for the pattern (it is the `draft` chain's oracle).
