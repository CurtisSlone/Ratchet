# Composition

Composition turns a folder of specs into a working multi-file system. This page explains why it works and
where its limits are. For the steps, see [Compose from specs](../how-to/compose-from-specs.md).

## The idea

Build the units in DEPENDENCY ORDER, and generate each one against the code already built - not against a
guess of what the other units look like. A data type is built first; the store that uses it is built next,
seeing the type's real API; the entry that wires them is built last, seeing both. Each step is
Oracle-checked (it must build) before the next one starts.

## The multi-reference frontier (the main limit)

The reliability of a generated unit scales INVERSELY with how many OTHER units it must call at once:

- **0 to 1** other units referenced: reliable - it composes first try.
- **2 or more**: the drift zone - the model gets the others' exact signatures wrong (a constructor's
  argument count, a method's name) even when it can see the project.

This is not about the entry unit specifically; any unit that references several others can drift.

## How Ratchet closes it

- **Bind the real API.** Before generating a unit, `project_api` extracts the public surface (types,
  constructors, method signatures) of the units already built and puts it in front of the model as the
  authoritative list: use these names and signatures verbatim; if the spec says something different, the
  built code wins. This removes most signature drift.
- **Interfaces are the lever.** An interface collapses many concrete contracts into one. A unit that
  depends on an interface holds a single, stable contract instead of N - so it stays in the reliable zone.

## Keep the entry thin (a corollary)

The entry unit should do ONE thing: construct the components and wire them together. When a single unit
is both the program entry (it gets `func main`) and a substantial component (a type with methods), two
costs stack on the same file: it is a multi-reference unit (it calls several others) AND it mixes a type
definition with `func main`. That is the unit most likely to drift and to leave a trailing unused import
or variable that a single repair does not clean.

Observed: a URL-shortener composed cleanly for its data type, encoder, and service, but the unit specced
as both "the HTTP server" and "the entry" failed - the generated `main.go` carried an unused import and
an unused local that survived its one repair. The data/component units around it built first try.

The lesson is upstream, in how the system is decomposed: spec exactly ONE small entry whose only job is
wiring, and make the server (or other heavy logic) its own component. A thin entry references the others
but defines almost nothing itself, so it stays cheap to generate and cheap to repair.

## The repair budget

Each composed unit gets a BOUNDED repair (the reference compose repairs once, then aborts the unit).
That is right for data and single-reference units, which rarely need it. A unit at the multi-reference
frontier - especially a fat entry - can need more than one round, and the leftover is usually trivial (an
unused import or variable). Three ways to stay inside the budget, in order of preference: decompose so no
unit is both entry and component (above); give the compose deeper repair (a second `fix`/`rebuild` pair,
as the C# `add_file` does); or accept the partial result and close it with one `edit_file` on the unit
that did not finish. The compiler names the exact leftover, so the corrective prompt is short.

## The Oracle gates contracts, not behavior

The build checks that the code LINKS - right names, right signatures. It does NOT check that the code does
the right thing. So a composed system can build cleanly and still misbehave (for example, wiring the wrong
values together). That gap is closed by a human in the loop: review the result, then give one corrective
prompt (an `edit_file` on the unit that is wrong). The compiler is the contract oracle; the author is the
behavior oracle.

## What composes easily

- Pure data types and single-reference components compose first try.
- Concurrency lives inside a component (a thread-safe class), so it composes like any other unit.

The hard cases are the multi-reference units above - which is exactly what `project_api` and interfaces
address.

Lineage note: composition is built on the engine's generic `foreach` step; everything else - the spec
convention, `project_api`, the plan - lives in the ratchet. See [Architecture](architecture.md) for the
host/ratchet split.
