# Base Doc: A Zero Trust Reference Architecture for Event-Driven AI Agents

The high-level spine. This is the map we reference and keep in sync as we expand each section. The full prose
draft lives in `ZeroTrust_EventDriven_AI_Agents_Reference_Architecture.md`.

---

## Thesis

The industry agreed agents should be event-driven. The events have to be verifiable, or the autonomy is
unauditable. **One-liner: do it where it can be proven.**

## Positioning (honest synthesis, not novelty)

This is an assembly of mature, respected primitives pointed at a new target. The space is crowded as of 2026:
hash-chained agent audit trails, signed action-attestation, CaMeL/FIDES-style model confinement, and SPIFFE-based
agent identity all already exist. Assume the individual primitives are claimed. The value is the coherent,
vendor-neutral synthesis plus an explicit account of what it does not cover.

- **vs Confluent / Solace / AWS event streaming:** they sell speed, scale, decoupling. Events stay ephemeral messages.
- **vs DID/VC agent-identity crowd:** they verify the actor, not the event.
- **vs "trusted agentic AI" governance:** approval gates, not cryptography.
- **vs CaMeL / FIDES / dual-LLM:** they confine an untrusted model (control/data-flow integrity). We credit that
  lineage; we reframe it as Zero Trust's implicit-trust-zone relocated to the context window.
- **vs in-toto / SLSA / Rekor:** same crypto, different target. They attest build pipelines; we attest agent actions.
- **The two genuinely less-unified flags (where we plant):**
  1. **One signed-event contract for humans and agents** (most work secures only the agent).
  2. **The event stream itself as the signed substrate** (vs a per-agent log beside the system); provenance of the
     trigger and the action, not just the actor or the output.

## Guiding principles (load-bearing)

1. Zero Trust applied to events and actions, not just subjects and devices.
2. Events are signed facts, not messages. Verifiable without trusting the producer.
3. Determinism in the trust path. The model proposes; a deterministic policy decides; enforcement is deterministic.
4. Least privilege bound to the action (single-use, per-action grants), not standing power held by the actor.
5. Enforcement guarantees provenance: routed to the path that can prove what happened, not blocked for lack of permission.
6. Defense in depth, blast-radius separation (report vs enforce by deployment, not convention).
7. One contract for humans and agents.

## Duties (the obligations, was "Goals")

Reframed from goals to duties: obligations the system is bound to uphold, not targets it optimizes toward. Fits a
control architecture, where you do not trust a component to optimize its way to the right answer.

1. Verifiable autonomy (any action reconstructable/verifiable by a party trusting none of the actors).
2. Tamper-proof history (append-only, provably so).
3. Deterministic, defensible decisions (every allow/deny reproducible, tied to the policy that fired).
4. Containment (a compromise cannot exceed a pre-authorized blast radius).
5. Incremental adoption (layers onto existing LangGraph/MCP/streaming stacks).

**Separation of duties (NIST 800-53 AC-5):** model proposes, PDP decides, PEP acts, log proves. No actor (human,
agent, or model) holds end-to-end authority over an action. The proposer is never the decider, the actor, or the
prompt author.

## Core artifact: the signed action-event

The unit of the system. A fact re-verifiable by anyone without trusting the emitter. Fields:

- actor (cryptographic identity) · action (verb + target) · context (channel/path, location, justification)
- input hash · decision reference (signed PDP token + policy) · replay hash (deterministic, identity-free)
- **prior reference** (hash of the preceding event in the chain, so a multi-step action is an ordered hash-linked sequence)
- signature + append-only-log inclusion proof

Verification chain (no model in it): check signature → recompute replay hash → confirm link to prior event →
confirm log inclusion → match decision token.

## Components (NIST 800-207 + substrate)

- **PIP**: source of signed context/evidence; never serves an unverifiable attribute. Also the only source of
  trusted *instructions* for prompt construction.
- **PDP**: deterministic engine (rules / OPA [11] / Cedar [12]) → allow/deny + signed decision token. Guarantee is
  bounded by policy expressiveness; what policy cannot express routes to a human, never back to the model.
- **PEP**: three independent checks (identity scope, signed decision token, gate stack); routes to provable paths.
- **Substrate**: streaming bus (Kafka [14] or equiv) carrying signed facts. Regime: high-value, lower-frequency
  action events, not a telemetry firehose.

## Verifiable log (3.4, own section)

Tamper-**proof** in the precise sense: any alteration/deletion/reorder is detectable and provable by a party
trusting none of the actors (detection + non-repudiation, not byte-level prevention; not zero-knowledge). RFC 6962
[3] / Sigstore Rekor [6] pattern; history-tree proofs from Crosby-Wallach [7].

- **Signed tree head (STH)**: signed Merkle root + size; commitment to the whole set.
- **Inclusion proof**: an event is in the log (O(log n)), verifiable without trusting the operator.
- **Consistency proof**: earlier STH is a prefix of a later one: append-only, nothing deleted/reordered/rewritten.
  (Inclusion alone is insufficient: an operator can rebuild the tree and still pass inclusion.)
- **Chained + witnessed heads**: per-event hash chain + STHs gossiped to witnesses to defeat equivocation.
  Honest limit: intra-org witnesses share a trust root/admin; cross-org or third-party witnessing is what closes it.
- **Action-chain integrity**: the run (start + ordered steps + outcome) becomes one hash-linked, signed chain whose
  head is a leaf in the tree. Structurally in-toto [4] / SLSA [5] applied to agent actions, not build stages.

## Model boundary (3.5, was "the hard line")

Two halves: where the model sits, and how it is fed.

- **Placement**: model is NEVER in: the evidence path, the allow/deny decision, the enforcement action. ONLY in
  proposal and triage. Proposes; deterministic policy decides; destructive actions get extra gates.
- **Context window is a trust zone**: everything in the prompt is read with uniform authority; that is the implicit
  trust zone ZT exists to abolish, relocated from network to prompt. Lineage: dual-LLM [9] / CaMeL [8] / FIDES; we
  reframe it as a ZT trust-zone problem.
- **Prompts are built by a trusted component, not accumulated**: a deterministic constructor (flows/chains/host
  code) emits the prompt; the model never authors its own. Instructions only from a verified, provenance-bearing
  source (PIP); untrusted content admitted strictly as data (delimited, never instruction). Per-fragment trust on
  who/what/where (same triple as the signed event). Honest: delimiting is discipline; the hard guarantee is
  propose-into-constrained-slot + deterministic decide.
- **Constrained output**: model emits into an enum/schema slot, not free-form; nothing it produces authorizes action.

## Nested orchestration: the orchestrator is an agent (3.5.5)

A frontier model driving a narrower/local one over MCP is common and efficient, and it is a trust trap.

- **Orchestrator is an agent with a duty**, not a privileged outside controller. It can only invoke declared,
  scoped tools (blast radius = the declared tool surface, not the model's imagination).
- **It must attest** verifiably: standard **IAM** (scoped identity from the common authority) bound to the
  **context of execution** (the channel/path = the event's Context field). Identity = who; execution context =
  through-which-path; together = the provenance the system verifies. Same contract as a worker or human.
- **Context isolation = a trust-zone boundary.** Multiple agents = multiple context windows = multiple trust zones;
  context does not cross. Privacy + blast-radius control; structural isolation, but privacy is only as good as data
  minimization at the interface (scoped tasks in, results not raw data out).
- **Rule: isolate context, unify identity and enforcement.** Separate what each agent knows; share who they are and
  what they may do via a common auth + execution layer (the shared MCP). Each handoff is an A2A edge, verified.

## Operational loop (each step emits a signed event, bound to the prior → hash-linked chain)

1. **Sense**: verify event signature + replay hash at ingest; reject the unverifiable.
2. **Reason**: agent reasons over the signed event (pulls signed evidence if needed); produces a proposal.
3. **Decide**: deterministic PDP → allow/deny + signed token; high blast radius hits human-in-the-loop.
4. **Act**: PEP verifies scope + token + gates; routes to a path that can prove it (single-use token for agents).
5. **Prove**: action emits a signed result, bound to its chain, into the verifiable log; new proof, next loop.

## Actors

Humans and AI agents emit the same signed action-event. The control plane does not care who acts; it cares the
action is attributable, decided deterministically, and proven.

## Framework mapping (the agent-chain context)

- **LangChain / LangGraph**: authored graphs, one per event type; model in reasoning nodes; decision/enforcement
  nodes deterministic; each edge a deterministic transition, each node emits a signed event (the action chain);
  checkpointing for durability; interrupt = the human approval gate.
- **MCP**: each tool call is a signed action-event under the PEP; servers split by blast radius, scoped identities.
- **A2A**: agent-to-agent (incl. orchestrator→worker) messages ride the bus as signed events; verified like any
  other, so an injected/impersonated agent message cannot propagate.
- **Substrate**: Kafka or equiv; signed events make durability a tamper-proof history.

## Coverage and scope (3.9)

- **Directly addressed:** prompt injection reaching action (prevented at boundary + contained), insecure tool use,
  non-repudiation/audit, unreliable output, runaway cost.
- **Partial (substrate, not active control):** action-path exfil yes / output-channel exfil no; audit substrate yes
  / active monitoring no; detection-enabled / not detection-implemented.
- **Out of scope:** model supply chain (training data, model provenance) and model hardening (jailbreak/DoS). We make
  a fooled model unable to act without a deterministic decision and an indelible record; we do not make it harder to fool.

## Prior work and citations

Name the mature tech explicitly at point of use (assembly of respected parts, not invention): NIST 800-207 [1] /
800-53 [2]; RFC 6962 [3] / Rekor [6] / Crosby-Wallach [7]; in-toto [4] / SLSA [5]; CaMeL [8] / dual-LLM [9] / FIDES;
SPIFFE/SPIRE [10]; OPA [11] / Cedar [12]; OpenTelemetry [13]; Kafka [14]; MCP [15] / A2A [16]. IEEE numbered
references in the full draft. Freshest agent-specific works (FIDES, hash-chained agent runtimes, CSA Agentic Trust,
NIST NCCoE agent identity, OWASP Agentic Top 10, AIMS) are cited by name; verify exact links before publishing.

## Standing constraints (apply to every section)

- Vendor-neutral. No ProofLayer or ESP names; "provenance" stays a lowercase concept, not a product. Generic concepts only.
- Honest labeling: describe patterns, not shipped products. The crypto verifiable log is the planned next layer of
  the working reference implementation, not yet built. Position as synthesis, not novelty.
- "Tamper-proof" is used, defined precisely on first use (detection + provable, not byte-prevention).
- Cite mature, respected tech explicitly (IEEE). Enforcement is recommend-first, opt-in, adopted last.
- No em dashes. Practitioner voice, defensible claims, no hype.
- Ground everything in real agent tooling (LangChain/LangGraph/MCP/A2A/Kafka).

## Section map and status

| # | Section | Scope | Status |
|---|---------|-------|--------|
| 1 | Introduction | overview, scope, audience, authoritative sources, prior-work positioning (1.5) | drafted |
| 2 | Context | problem space (5 dims), principles, duties + separation of duties, constraints | drafted |
| 3.1 | Ecosystem & Actors | the four roles; producers/consumers | drafted |
| 3.2 | Signed action-event | the event contract; carries a prior-event reference (chain link) | drafted, technical heart |
| 3.3 | Components | PIP/PDP/PEP/substrate (log split out to 3.4); policy-expressiveness + regime notes | drafted |
| 3.4 | The Verifiable Log | STH, inclusion + consistency proofs, chained/witnessed heads, action-chain integrity, honest boundary | drafted (design target; not yet implemented) |
| 3.5 | The Model Boundary | placement + context window as trust zone + trusted prompt construction + constrained output | drafted |
| 3.5.5 | Nested orchestration | orchestrator as agent; IAM + context of execution; context isolation; isolate-context-unify-enforcement | drafted |
| 3.6 | Crosscutting | identity/PKI (SPIFFE), single-use tokens, blast-radius, observability (OTel), ingest verification | drafted |
| 3.7 | Operational loop | sense/reason/decide/act/prove, as a hash-linked chain | drafted |
| 3.8 | Framework mapping | LangChain/LangGraph/MCP/A2A/substrate | drafted |
| 3.9 | Coverage and Scope | directly addressed / partial (substrate not active control) / out of scope | drafted |
| 4 | Threat model | injection (prevented + contained), log tampering, equivocation; residual gaps named | drafted |
| 5 | Implementation guidance | incremental retrofit order | drafted |
| 6 | Conclusion | the close | drafted |
| - | References | IEEE [1]-[16]; fresh agent-specific works named, links to verify | drafted |

## How we use this doc

This base is the source of truth for the architecture's decisions and language. When we expand a section, we
work against this map, keep the principles and the event contract consistent, and update the status column. If a
section pass changes a core decision, change it here first, then ripple it into the full draft.
