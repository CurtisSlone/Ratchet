# AGENTS.md - orientation + routing for AI agents

You're an AI agent (any model) working on or with **Ratchet** (this repo = the host engine `ratchet`,
a cross-platform Go binary). **The one rule:** keep domain logic in a *ratchet*, never in the engine
(`go_src/`) - a new capability is a new flow or tool, not a host change. Now route from the tables; the
orientation is below. Humans: [README.md](README.md).

## Route by what you're doing

**Working on a ratchet** - terse, procedural playbooks in `docs/agents/`:

| Task | Go to |
|---|---|
| Iterate on a ratchet (the loop: orient -> change -> verify -> fix) | [Iterate on a ratchet](docs/agents/iterate-on-a-ratchet.md) |
| Add or change a flow (chain) | [Edit a flow](docs/agents/edit-a-flow.md) |
| Add or change a tool | [Edit a tool](docs/agents/edit-a-tool.md) |
| Build a multi-file system from specs | [Compose a system](docs/agents/compose-a-system.md) |
| Start a new ratchet | [Build a ratchet](docs/how-to/build-a-ratchet.md) |
| Drive a ratchet to build something (you are the operator) | [Drive a ratchet](docs/agents/drive-a-ratchet.md) |

**Editing the host engine** (`go_src/`): [Work on the host](docs/how-to/work-on-the-host.md) - build,
verify, layout, the gotchas.

**Reference and concepts:**

| Want | Go to |
|---|---|
| The flow format / node kinds | [Author flows](docs/how-to/author-flows.md) |
| The tool contract | [Author tools](docs/how-to/author-tools.md) |
| The manifests (tools / flows / kb) | [Build the manifests](docs/how-to/build-manifests.md) |
| How the knowledge base works / build one | [Knowledge bases](docs/how-to/knowledge-bases.md) |
| The spec + compose format | [Compose from specs](docs/how-to/compose-from-specs.md) |
| How and why it works | [Architecture](docs/concepts/architecture.md), [Context Binding](docs/concepts/context-binding.md), [Composition](docs/concepts/composition.md), [Observability](docs/concepts/observability.md) |
| Drive a ratchet (console / MCP) | [Use the console](docs/how-to/use-the-console.md), [Drive over MCP](docs/how-to/drive-over-mcp.md) |
| Look up a term | [Vocabulary](docs/Terms.md) |
| Is a ratchet safe to open? | [Security](SECURITY.md) |

## What Ratchet is (the 5 Ws, short)

A cross-platform host (a single static Go binary) that runs a small **local** model as a *constrained
proposer*: the model proposes
into a fixed chain of steps; a deterministic **Oracle** (a compiler, a parser, a table validator) accepts
or rejects each step; the chain advances only on a pass. The host is a domain-agnostic harness - all
domain logic lives in the **ratchets** it loads. Use it for bounded, *verifiable* generation, not
open-ended roaming. A human drives from the console, or a frontier model drives over MCP; the local model
never picks actions, it only fills slots.

## The two ideas (know them by name)

- **The Oracle** - deterministic verify-then-advance with bounded repair. An Oracle pass means "won't
  break," not "is correct."
- **Context Binding** - each chain node sees ONLY its declared, scoped inputs (a prior output, a fixed
  `ref`, a `search` hit) - never a cumulative tape. Isolation is the biggest reliability lever.

Lineage: structure-as-architecture is from ICM; RAG is a technique; the action-chain + Context Binding
model is Ratchet's own. Don't call Ratchet "just ICM." Why the boundary matters:
[Architecture](docs/concepts/architecture.md).

## Before you touch `go_src/`

Build and verify after any host change - full detail, repo layout, and the constraints that bite are in
[Work on the host](docs/how-to/work-on-the-host.md):

```
make build        # -> bins/<os>-<arch>/ratchet   (cd go_src && go build under the hood)
make test         # the Go unit tests
ratchet selftest  # the deterministic core, model-free (also `make smoke`)
```

These **bite**: keep it **pure Go, `CGO_ENABLED=0`** so it cross-compiles to every target with no C
toolchains - do not add a cgo dependency. Go forbids import cycles (the old C# build was one flat
namespace), so shared types live in `internal/model` and constants in `internal/conventions`; leaf
packages depend inward. Run `gofmt`/`go vet` clean. The original C# host is kept under `csharp_src/`
for reference and as the SAC-friendly Windows build; do not edit it for new behavior. See
[Build the legacy C# host](docs/how-to/build-csharp-host.md).

## Style

Plain and grounded: no emoji, no em dashes, no hype. Verify, don't assert - compile-/run-verify and show
the evidence; "compiles" is not "behavior-correct."
