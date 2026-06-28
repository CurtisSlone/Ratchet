# Development methodologies (and what generated code teaches us)

There is more than one way to drive verifiable code generation in Ratchet. Each is an arrangement of
`generate` steps plus a deterministic Oracle - not a philosophy, a topology. This page covers the two
that are proven, the ladder that unifies them, and the engineering lessons that hold across both. For the
multi-file composition mechanics specifically, see [Composition](composition.md).

## Two methodologies

**Spec-driven** (the `compose` model). A folder of specs becomes a working system: build the units in
dependency order, each generated against the code already built, each Oracle-checked (it must build)
before the next. The test is generated alongside the implementation. See [Composition](composition.md).

**Test-driven** (the assurance-ladder model). The test is authored FIRST, from the spec's behavior, and
becomes the contract the implementation must satisfy:

1. **Stub** - signatures only, `panic` bodies. Oracle: it compiles (the type-driven rung).
2. **Test (red)** - author the test (and a property/fuzz target). Oracle: it COMPILES against the stub
   AND FAILS. A test that passes against panic bodies asserts nothing and is rejected.
3. **Impl (green)** - fill the bodies. Oracle: `go test -race` passes. Failures feed back as repair.
4. **Harden** - the full gate (vet/staticcheck/govulncheck).

The difference that matters: spec-driven trusts a test generated next to the code; test-driven proves the
test is meaningful (red) before any code exists, then drives the code to green. Test-driven costs more
steps and buys more assurance.

## The assurance ladder

The methodologies are rungs of one ladder of increasing Oracle strength over the same code:

```
types (compiler) -> examples (go test) -> properties (fuzz / -race) -> harden (vet/staticcheck/govulncheck)
```

A task climbs as far as it needs. A plain data helper stops at types+examples; a concurrent type climbs
to properties; anything shipped gets hardened. The property/fuzz rung is the one that catches what a build
gate cannot: `go test -fuzz` and `-race` exercise the UNEXERCISED paths - the gap behind "it compiles and
the happy-path test passes, so it must be correct". (This is exactly the class of bug - an expiry race, a
large-count overflow - that slips through a build-only gate.)

## Lessons that hold across both

- **Green means "won't break", not "is correct".** An Oracle pass certifies the verified properties, not
  intent. The stronger the rung, the closer green gets to correct - which is why the ladder exists.
- **A meaningful test must be proven, not assumed.** Generating test and code together lets a weak test
  pass trivially (false green). The red gate - compile-then-fail against a stub - is what gives a test
  teeth. If you only take one idea from test-driven, take this.
- **Determinism beats prompts for mechanical errors.** A small local model will violate explicit prompt
  rules ("import only what you use", "one package", "no entry point here"). Do not rely on the model's
  care for mechanical correctness - fix the class deterministically (e.g. strip unused imports after
  generation) so every flow benefits.
- **Bind the real contract; don't ask the model to remember it.** Put the already-built API in front of
  the model verbatim (the built code wins over the spec). The same instinct drives the red gate (the
  stub is the contract the test must match) and Composition's `project_api`.
- **Bound the model; fail clean; ship nothing broken.** Make a strict Oracle productive with a feedback
  cycle (re-generate with the verdict), and bound it with a repair cap (K attempts, then a clean failure)
  so an unsatisfiable gate fails fast instead of spinning. Across hard runs the win is not a perfect
  success rate - it is that the Oracle rejects every bad attempt and ships nothing. That guarantee is the
  product.

## The capability frontier: structure first, then a bigger model

Reliability falls as the number of interdependent pieces a step must hold at once rises (Composition's
multi-reference frontier is the same effect). Two distinct walls, in order:

1. **Structural walls yield to structure.** A multi-type system fails when the model scatters the pieces
   across inconsistent packages so they cannot reference each other. The fix is to remove the choice -
   one package at the root - not a bigger model. Bind the real API, prefer interfaces, keep a type's data
   and methods in one unit. These push the ceiling up without spending capability.
2. **Past structure lies raw code-gen reliability.** Once the pieces are coherent, a sufficiently complex
   artifact (e.g. a concurrent, fuzzed, multi-type test) still defeats a small model - a different trivial
   error each attempt, where prompt and structure have diminishing returns. This is the "bigger model OR
   much more structure" line. The Ratchet-native lever is to shrink what the model writes at once
   (generate per aspect, each gated); the alternative is a stronger generate model for that tier.

The practical takeaway: spend structure first - it is cheap and it is where most failures actually live -
and treat a stronger model as a deliberate per-tier choice, not the default reach.
