# flows/

Action chains. Each chain is a directory:

```
<chain>/
  chain.json          { id, summary, entry, inputs?, budgets, nodes[] }
  actions/<node>/
    action.json       { id, kind, inputs[], edges }
    prompt.md         model instructions ({{ slot }}) - for generate / ai_branch
```

Node `kind`s: `action` (run a tools/ script + validators -> on_success/on_failure), `generate`
(free text via the generate seat -> on_success/on_failure), `ai_branch` (slots -> prompt -> enum
`next` -> transitions), `summarizer`, `exit` (outcome).

Input binding sources (a node sees ONLY its declared inputs): `from` a prior node or a run seed
(`$input`, `$workspace`, or a chain-declared `inputs` slot); `ref` a fixed kb entry; `search` a kb
query (templated). Cap injected slots with `max_chars`.

Run with `/flow <chain> <input>` (or let `/route` pick one by `summary`). Lint with
`ratchet validate-flow <dir>`. The `draft` chain here is a generate->check->repair example; the
bundled C# instance (`examples/dotnet`) has richer ones (`flows/csharp`, `flows/edit_file`).
