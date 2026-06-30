# Structural Assurance for Event-Driven Agentic AI: A Zero Trust Reference Architecture

**Making the agent loop verifiable, one gated action at a time**

ScanSet Inc

---

## Table of Contents

1. Introduction
   - 1.1 Overview
   - 1.2 Scope
   - 1.3 Audience and Expectations
   - 1.4 Key Authoritative Sources
   - 1.5 Relationship to Prior Work
2. Context
   - 2.1 Problem Space
   - 2.2 Guiding Principles
   - 2.3 Duties
   - 2.4 Constraints and Assumptions
3. Architecture
   - 3.1 Ecosystem and Actors
   - 3.2 The Signed Action-Event (the core artifact)
   - 3.3 Components (PIP, PDP, PEP, substrate)
   - 3.4 The Verifiable Log
   - 3.5 The Model Boundary
   - 3.6 Crosscutting Concerns
   - 3.7 Operational Overview (the provable loop)
   - 3.8 Applying It to Agent Frameworks
   - 3.9 Coverage and Scope
4. Threat Model
5. Implementation Guidance
6. Conclusion

---

## 1. Introduction

### 1.1 Overview

The industry has reached consensus on one point: production AI agents are event-driven. Frameworks such as LangChain and LangGraph, multi-agent orchestrators, the Model Context Protocol (MCP), and the Agent2Agent (A2A) protocol all converge on the same shape. An agent senses an event, reasons over context, calls tools, hands work to other agents, and acts on the world, asynchronously and at its own pace, usually over a streaming substrate like Kafka.

That shape solves latency and scale. It does not solve trust. In nearly every published event-driven agent architecture, the events themselves are treated as ephemeral messages. The message is consumed, the agent acts, and what remains is a log written after the fact, mutable and beside the system rather than part of it.

This reference architecture closes that gap. It applies Zero Trust, in the NIST SP 800-207 sense of "never trust, always verify," not only to the actors in an agent system but to the **events and actions** that drive it. The central claim is simple: if the agent loop is event-driven, the events have to be verifiable, or the autonomy is unauditable. The mechanism is to make every event a signed, replayable fact, to keep the decision and verification path deterministic, and to record every decision and action as proof. The assurance is structural. The same gate that authorizes an action produces its evidence, so an action that cannot be proven cannot be taken. The one-line statement of intent is: **do it where it can be proven.**

### 1.2 Scope

This document describes a vendor-neutral architecture for securing event-driven, multi-step AI agent systems ("agent chains") with Zero Trust principles. It covers the event contract, the policy decision and enforcement model, the verifiable log, the placement of the language model relative to the trust boundary, the crosscutting concerns, and the operational loop. It maps the architecture onto common agent tooling (LangChain, LangGraph, MCP, A2A) and an event-streaming substrate.

It is deliberately out of scope to specify a particular model, framework, cloud, or message broker. The architecture is a pattern, implementable incrementally on top of stacks teams already run.

### 1.3 Audience and Expectations

The audience is the engineer or architect building or securing agent systems that take real action: opening tickets, moving money, changing access, touching production. Readers should expect a control-plane design, not a model-training guide. The architecture assumes the hard problems are not the model's reasoning quality but the trust, attribution, and auditability of an autonomous system that acts.

This is a reference architecture, not a product. Every component below can be satisfied by more than one implementation, open-source or commercial.

### 1.4 Key Authoritative Sources

- NIST SP 800-207, *Zero Trust Architecture* [1], for the Policy Decision Point (PDP), Policy Enforcement Point (PEP), and Policy Information Point (PIP) model.
- NIST SP 800-53 [2] control families for audit (AU), access control (AC), and identification and authentication (IA), including separation of duties (AC-5).
- The Merkle-tree transparency-log pattern (standardized for certificate transparency in RFC 6962 [3], deployed in production by Sigstore's Rekor [6]) for tamper-proof, append-only records, with inclusion and consistency proofs.
- The Model Context Protocol (MCP) [15] and the Agent2Agent (A2A) protocol [16] for agent-to-tool and agent-to-agent interaction.
- Established event-driven architecture (EDA) practice, for example Apache Kafka [14], for the streaming substrate.

### 1.5 Relationship to Prior Work

This architecture is an assembly of mature, respected components pointed at a new target. Almost nothing in the mechanism is novel, and that is deliberate: the assurance comes from primitives that are already deployed and trusted, not from invention. The lineage, by layer:

- **Zero Trust model.** The Policy Decision, Enforcement, and Information Points are NIST SP 800-207 [1]; the audit, access, and separation-of-duties controls are NIST SP 800-53 [2].
- **The verifiable log (3.4).** The Certificate Transparency pattern (RFC 6962 [3]), as deployed in production by Sigstore's Rekor [6] on a Merkle log. Inclusion and consistency proofs trace to Crosby and Wallach's tamper-evident history trees [7].
- **The action chain (3.2, 3.4.2).** A chain of signed, hash-linked step records is structurally an in-toto [4] / SLSA [5] attestation chain, the software-supply-chain provenance pattern, applied to an agent's actions instead of a build pipeline.
- **The model boundary (3.5).** Confining an untrusted model by control- and data-flow integrity is the dual-LLM pattern [9] formalized by CaMeL [8] and extended by information-flow systems such as Microsoft's FIDES. The contribution here is not the confinement; it is reframing it as Zero Trust's implicit-trust-zone problem relocated to the context window.
- **Identity (3.6).** Per-actor scoped cryptographic identity from a common authority is SPIFFE/SPIRE [10], now the emerging baseline for agent identity (recommended in NIST's 2026 NCCoE work on AI agent identity and mapped onto agent stacks by efforts such as AIMS).
- **Policy, substrate, observability.** Deterministic policy is OPA [11] or Cedar [12]; the substrate is Kafka [14] and standard event-driven practice; observability is OpenTelemetry [13].

This is a crowded and fast-moving space. By 2026, hash-chained, tamper-evident audit trails for agent actions exist in running systems, signed action-attestation patterns have been described, and Zero Trust frameworks for agents (the Cloud Security Alliance's Agentic Trust Framework, the OWASP Top 10 for Agentic Applications) are converging on identity, segmentation, and auditability. A reader should assume the individual primitives below are claimed.

Three things in this document are less commonly unified, and they are where it plants its flag:

1. **Assurance that is structural, not observed.** The deterministic check that produces the evidence is the same gate that advances the action: you cannot take the action without producing the proof, because the proof-producing step is the control flow, not a logger beside it. Remove the gate and there is no chain. Other designs instrument an agent for observability after the fact; here the assurance is load-bearing by construction.
2. **One signed-event contract for humans and agents.** Most agent-security work secures the agent. Here a human operator and an autonomous agent emit the same signed action-event, decided by the same deterministic policy, and recorded in the same log. The control plane does not care who acts; it cares that the action is attributable, decided, and proven (2.2.7).
3. **The event stream itself as the signed substrate.** Rather than a per-agent log written beside the system, the events on the streaming bus are the signed facts, and the provenance covers the trigger as well as the action (3.2). The audit trail is the system, not an addition to it.

None is a new primitive; each is a particular composition. The value of this document is the coherent, vendor-neutral synthesis of respected parts, plus an explicit account of what it does not cover (3.9).

---

## 2. Context

### 2.1 Problem Space

An event-driven agent system inverts the classic request/response model. Instead of a user calling a function and waiting, agents subscribe to streams of events and act when something happens. This is what makes them scale, and it is also what makes them dangerous when left unverified. The problem space has five dimensions.

**2.1.1 The event is the attack surface.** In an autonomous loop, whatever can place an event on the bus can drive the agent. A forged or replayed event is an instruction the agent will faithfully execute. Prompt injection stops being a chat-window curiosity and becomes an event-injection attack against a system that takes action.

**2.1.2 The event is the only audit trail.** When an agent chain spans many nodes, tools, and other agents, the only record of what happened is the sequence of events. If those events are ephemeral messages, post-incident forensics depends on whatever logging was bolted on, which is mutable and rarely complete.

**2.1.3 Non-determinism in the decision path.** A language model is non-deterministic by design. The moment a model's output is what authorizes an action, you can no longer reproduce, verify, or defend why the system did what it did. Correctness becomes a matter of confidence, not proof.

**2.1.4 Permission and tool sprawl.** Agent frameworks make it trivial to hand an agent broad, standing credentials and a large toolbox. Standing power in an autonomous process is the opposite of least privilege. A compromised or confused agent with broad write access is a self-inflicted incident waiting to happen.

**2.1.5 Two kinds of actor, one missing contract.** Humans and agents increasingly act through the same systems. A privileged operator and an autonomous agent may both call the same API. Most designs secure these separately, or secure the human and forget the agent. They need one verifiable contract for action.

### 2.2 Guiding Principles

**2.2.1 Zero Trust, applied to events and actions.** Never trust an event, an actor, or an action by virtue of where it came from. Verify each one against signed evidence. This extends 800-207 from "verify the subject and device" to "verify the event and the action."

**2.2.2 Events are signed facts, not messages.** The unit of the system is a signed, self-describing event that can be re-verified later by anyone, without trusting whoever produced it. Verifiability without trust in the producer is the property that everything else rests on.

**2.2.3 Determinism in the trust path.** The model may propose. A deterministic policy decides. Enforcement is deterministic. No non-deterministic component sits anywhere a wrong answer authorizes an action. This is the hard line that makes the system defensible. What this makes reproducible is the authorization, not the behavior: when several actions are policy-allowed, the model still selects among them, so determinism bounds the action space and the verdict, not the model's choice within it.

**2.2.4 Least privilege, bound to the action.** Capability is granted per action, not held standing by the actor. An autonomous process should never carry broad, long-lived write power. It should receive a narrow, single-use grant for one approved action.

**2.2.5 Enforcement guarantees provenance.** The purpose of enforcement is not only to block bad actions. It is to ensure actions occur only on paths that can prove what happened. You are routed to the path that produces a verifiable record, not blocked for lack of permission.

**2.2.6 Defense in depth and blast-radius separation.** Non-destructive actions and destructive actions are separated by deployment, not by convention. A component that can only report cannot be coerced into enforcing.

**2.2.7 One contract for humans and agents.** A human operator and an AI agent emit the same kind of signed action-event. The control plane does not care who acts. It cares that the action is attributable, decided deterministically, and proven.

### 2.3 Duties

This architecture is defined by its duties, the obligations it must uphold, rather than by goals it optimizes toward. The distinction is deliberate. A goal is a target a system pursues and may trade off; a duty is an obligation it is bound to honor. An autonomous system that acts on the world should be held to duties, because the whole premise here is that you do not trust a component to optimize its way to the right answer. You bind it to rules it cannot violate.

The control plane carries five duties:

1. **Verifiable autonomy.** Every action an agent takes can be reconstructed and verified after the fact, by a party that trusts none of the actors.
2. **Tamper-evident history.** The record of what happened is append-only and tamper-proof, provably so, not a mutable log.
3. **Deterministic, defensible decisions.** Every allow or deny is reproducible and tied to the exact policy that produced it.
4. **Containment.** A compromised agent, tool, or event cannot exceed a narrow, pre-authorized blast radius.
5. **Incremental adoption.** The architecture layers onto existing LangGraph, MCP, and streaming stacks without a rebuild.

These duties are also separated. No single component discharges more than one of them: the model proposes, the policy decision point decides, the enforcement point acts, and the log proves. This is **separation of duties** (NIST SP 800-53 AC-5) applied to an agent loop. No actor in the system, human, agent, or model, holds end-to-end authority over an action, so no single compromise carries one from intent to irreversible effect.

### 2.4 Constraints and Assumptions

**Constraints.** Signing, verification, and logging add latency and operational weight; the design must keep them off the model's reasoning path and on the action path, where the cost is justified. The architecture assumes a public-key identity system for actors and a durable append-only log.

**Assumptions.** Actors (humans, agents, services) can be issued cryptographic identities from a common authority. The organization can express its action policies as deterministic rules. Teams will adopt enforcement incrementally, starting in a recommend-only posture, before allowing automated destructive actions.

---

## 3. Architecture

### 3.1 Ecosystem and Actors

The ecosystem has four participant roles.

- **Human actors.** Operators, administrators, analysts. They act through interfaces and, sometimes, through privileged paths like a terminal.
- **Agent actors.** LLM-driven agents and the nodes of an agent graph (for example, the nodes of a LangGraph workflow). Each agent has its own scoped identity.
- **Tools and external systems.** The resources agents act upon: ticketing, identity providers, infrastructure, data stores, reached through MCP servers, A2A peers, or direct APIs.
- **The control plane.** The PIP, PDP, PEP, substrate, and verifiable log defined below. It is the trusted core that turns actions into proof.

All actors are producers and consumers of signed action-events on a shared event substrate. The control plane is the only component that is trusted, and it is trusted because it is deterministic and its outputs are themselves verifiable.

### 3.2 The Signed Action-Event (the core artifact)

The unit of this architecture is not the message. It is the event as a fact you can re-verify without trusting whoever emitted it. Get this contract right and the rest is plumbing.

Every action, human or agent, emits an event carrying:

- **Actor.** The cryptographic identity that produced it, issued by the common authority. A human through an authenticated session, an agent through its scoped identity, a service through its own. A signature, not a username in a field.
- **Action.** The operation: a verb and a target.
- **Context.** Channel and path (terminal, UI, API, MCP tool call, A2A message), location, and justification. This is the field that lets the same action be allowed down one path and refused down another, because only one path can prove what happened.
- **Input hash.** A binding commitment to what the action rests on: disclosed later, it proves the action used exactly that input. It proves equality, not properties, so for property-based policy the decision-relevant attributes are carried as signed values or PDP-attested predicate results rather than concealed behind a bare hash.
- **Decision reference.** The signed token from the deterministic PDP: allow or deny, and which policy fired.
- **Replay hash.** A deterministic, identity-free hash over intent and context (and, for idempotent actions, the recorded outcome), so the same intent under the same posture produces the same hash. It is for drift and tamper detection, not replay prevention: a legitimate repeat and a malicious replay hash identically, so replay is stopped separately, by freshness (a nonce or monotonic sequence) and the prior-reference chain, not by this field. For actions with external side effects the outcome is recorded and hashed, not reproduced.
- **Prior reference.** The hash of the preceding event in the same action chain, so a multi-step action is an ordered, hash-linked sequence and not a loose set of records (3.4.2).
- **Signature and log inclusion.** Signed over the canonical event, then appended to an append-only log that returns an inclusion proof.

The verification chain is fully deterministic and contains no model: check the signature against the issuing authority, recompute the replay hash, confirm the link to the prior event, confirm inclusion in the log, and match the decision token. A downstream consumer (a SOC, an auditor, another agent) runs the same checks and needs to trust none of the actors. The cryptography here is not novel and is not the point. Treating every action, human and machine, as first-class signed evidence rather than a log line written afterward is the point.

### 3.3 Components

Mapped onto the NIST SP 800-207 model, plus the event substrate. The verifiable log, central enough to warrant its own treatment, is the subject of 3.4.

**3.3.1 Policy Information Point (PIP).** The source of signed context the system reasons and decides over. State, posture, and evidence arrive as signed facts. The PIP never serves an unverifiable attribute. When an agent needs more context than the triggering event carries, it pulls signed evidence from the PIP, never ungrounded data. For a decision to be reproducible later, the PIP signs the attribute values used at decision time, not pointers to mutable sources, so a replay sees exactly the state the PDP saw. The PIP is also the only source from which trusted *instructions* may be drawn when a prompt is constructed (3.5.3).

**3.3.2 Policy Decision Point (PDP).** A deterministic engine (for example, a rules engine, OPA [11], or Cedar [12]) that takes a proposed action plus signed context and returns allow or deny, plus a signed decision token naming the policy that fired. The PDP is the only thing that authorizes action, and it is deterministic, so every decision is reproducible and defensible. The guarantee is bounded by what the policy can express: the PDP decides deterministically only over conditions that can be written as deterministic rules. Whatever the policy cannot express routes to a human (3.3.3), never back to the model. The system is as defensible as its policy is explicit, and the honest fallback for what policy cannot capture is a person, not a guess.

**3.3.3 Policy Enforcement Point (PEP).** The component that lets the decision take effect. The PEP enforces three checks independently: identity scope (is this actor granted this action on this resource class), a valid signed decision token (did the PDP ratify it), and a gate stack (severity and blast-radius thresholds, allowlists, human-in-the-loop approval for destructive actions, rate limiting, a global kill switch, fail-safe to deny on uncertainty). The PEP routes actions to paths that can prove them.

**3.3.4 Event substrate.** The streaming bus (Kafka [14] or equivalent) carrying signed action-events. It provides durability, replay, and decoupling. The architecture's contribution is that the substrate carries signed facts, not bare messages. The substrate provides durable transport and replay; the tamper-evidence comes from the verifiable log (3.4), not from the broker, which an administrator can still compact or delete. A note on regime: per-event signing, Merkle appends, and proof generation suit high-value, lower-frequency action events, the events that authorize action, not a high-throughput telemetry firehose. The cost of proof is justified where an action must be defended, not on every tick of the bus.

### 3.4 The Verifiable Log

The action chain needs an audit trail that is the system, not a log written beside it: append-only, and provably so to a party who trusts no one, the log's operator included. This is a solved problem, and the architecture consumes it rather than reinventing it.

**3.4.1 The log is a consumed component.** Technology such as an RFC 6962 [3] Merkle transparency log, deployed in production as Sigstore's Rekor [6], accomplishes this part. It gives the two properties an audit trail needs without trusting whoever runs it: an **inclusion proof** that a given action is in the record, and a **consistency proof** that the record has only ever grown, with nothing deleted, reordered, or altered. Gossiping the log's signed tree heads to independent witnesses additionally defeats equivocation, a log that shows one history to one verifier and a different one to another (cross-organization or third-party witnessing is what fully closes that, since intra-org witnesses share a trust root). The mechanism is standard, peer-reviewed, and out of scope here; the references carry it. What this architecture specifies is what gets appended, and under what contract.

**3.4.2 Integrity of the action chain.** A multi-step agent action is not one event but a chain: sense, reason, decide, act, prove (3.7). Each step is recorded as a signed event, and each binds the hash of the step before it, so the whole chain, from the triggering input through the model's proposal, the deterministic decision, the enforced action, and the outcome, is a single hash-linked sequence. Altering the proposal after the fact, or the decision that followed it, breaks every subsequent hash. The completed chain's head is the leaf appended to the log (3.4.1), so it inherits inclusion, consistency, and non-equivocation for the chain as a whole. The upgrade is concrete: an agent run that is otherwise a directory of plain step records, mutable and deletable with no trace, becomes an ordered, individually verifiable chain whose tampering cannot go undetected.

**3.4.3 What "tamper-proof" means, and the prior-art line.** The guarantee is detection and non-repudiation, not a claim that bytes on disk cannot be changed: any alteration, deletion, or reordering is detectable and provable by a party that trusts none of the actors, and with independent witnessing, undetectable tampering is computationally infeasible. For a record whose only job is to be trusted, that is the stronger property, you do not stop a dishonest operator from trying, you make trying useless. A fair question is how this differs from in-toto [4] or SLSA [5], which already sign hash-linked step metadata over a pipeline. In mechanism, it does not. The difference is the target and the contract: the steps are an agent's runtime actions rather than build stages, and the same signed contract covers human actors too (1.5). Applying the supply-chain provenance pattern to the agent's own action chain is the contribution, not the cryptography.

### 3.5 The Model Boundary

This is the load-bearing rule of the architecture, and it has two halves: where the model sits relative to the trust path, and how the model is fed.

**3.5.1 Where the model sits.** The language model is **never** in the evidence path, the allow/deny decision, or the enforcement action. It lives **only** in proposal and triage: classifying an event, drafting a response, suggesting a plan, summarizing context. It proposes; a deterministic policy decides; destructive enforcement passes extra gates. This placement is what makes putting an LLM in a control plane defensible to a security reviewer who would otherwise, and correctly, reject it. The model reasons only over signed evidence it cannot forge, and every action it proposes is converted by a deterministic PDP into a signed decision the PEP verifies. The agent cannot authorize itself. In LangGraph terms, the model occupies the reasoning nodes; the decision and enforcement nodes are deterministic and sit behind the human-in-the-loop interrupt. This is separation of duties (2.3) at the level of a single action: the component that proposes is never the component that decides or acts.

**3.5.2 The context window is a trust zone.** Zero Trust's founding move is to abolish the implicit trust zone, the network segment where, once you are inside, you are believed. NIST SP 800-207 exists to replace location-based trust with per-request verification. The context window of a language model is an implicit trust zone the agent world has not yet noticed it created. Everything placed in the window, the system instructions, retrieved documents, tool outputs, prior messages, and whatever a user typed, is read by the model with uniform authority. There is no inside or outside; there is only the prompt, and the model trusts all of it equally. That is precisely the implicit trust zone Zero Trust was built to eliminate, relocated from the network to the prompt. Confining an untrusted model by control- and data-flow integrity is the dual-LLM pattern [9] and CaMeL [8], extended by information-flow systems such as FIDES; what this section adds is naming it a Zero Trust trust-zone problem rather than a bespoke defense.

This applies across agents, not only within one. In a multi-agent system there is not one context window but several, one per agent, and each is its own trust zone. Context does not cross a zone boundary. An orchestrating agent and a worker it drives do not share state; the only thing that crosses between them is a scoped, typed interface call. Context isolation between agents is therefore a trust-zone boundary, enforced the same way as the boundary inside a single prompt: nothing crosses implicitly, only through a verified, scoped interface (3.5.5).

**3.5.3 Prompts are built by a trusted component, not accumulated.** The defense is to refuse the implicit trust zone and assign every fragment the trust level of its source. A prompt is not something that accumulates from whatever flowed in; it is an artifact emitted by a single trusted component, a deterministic constructor in the control plane, from sources of known provenance. The model is never its own prompt author. Two rules govern the constructor. First, **instructions originate only from a verified, provenance-bearing source**, the system's own authored, signed context from the PIP (3.3.1), never from retrieved content or user input. Second, **untrusted content is admitted strictly as data**: placed in a delimited, labeled slot, scoped to what the task needs, and never eligible to become instruction. A retrieved document can inform an answer; it can never issue a command. So prompt injection stops being a privilege escalation and becomes a trust-zone boundary violation: injected text arrives as data from an untrusted source, and the constructor gives untrusted data no path to instruction.

Two honest qualifications keep this from overclaiming. The delimiting is discipline, not a hard guarantee: a model can still be misled by content inside its data slot. What makes the boundary hold is the model boundary itself. The model only proposes into a constrained slot (3.5.4), and a deterministic component, not the model, decides and acts (3.5.1). Prompt hygiene lowers the odds of a bad proposal; determinism ensures a bad proposal cannot authorize anything. Together they are prevention at construction plus containment after. This is also the direct answer to the retrieval-integrity problem: the boundary a RAG pipeline must protect is exactly the line between trusted instruction and retrieved data.

**3.5.4 Constrained output.** The model's output is shaped as tightly as its input. Rather than emitting free-form text that downstream code parses for intent, the model proposes into a constrained slot: a choice from an enumerated set, a value against a schema, a single structured field. A grammar or schema constraint means the model can only emit a well-formed proposal, never a free-form instruction some later stage might act on. Combined with the boundary above, the model takes trusted-source instructions and untrusted data in, and emits only a bounded, checkable proposal out. Nothing it produces authorizes an action; the deterministic PDP and PEP downstream do that.

**3.5.5 Nested orchestration: the orchestrator is an agent.** A capable model often drives a narrower one. A frontier orchestrator offloads the mechanical work to a smaller or local agent over a scoped interface such as MCP, calling its tools and sequencing its steps. This is efficient, and it is also where a trust mistake hides. The orchestrator is not a privileged controller standing outside the system. It is an agent, and the architecture must treat it as one.

That has three consequences. First, the orchestrator carries a **duty** like any actor (2.3): it proposes into scoped capabilities, it does not get to decide or act outside them. Even a frontier orchestrator can only invoke declared, scoped tools and fill their arguments; it cannot invent an action, so the blast radius is the declared tool surface, not the model's imagination. Trusting the orchestrator because it is the capable one is the implicit-trust-zone mistake again, one level up.

Second, the orchestrator must be able to **attest** what it is and what it did, verifiably, so the rest of the system can confirm it without trusting it. This is standard identity and access management, a scoped cryptographic identity from the common authority (3.6.1), bound to the **context of execution**: the channel and path each action took, the Context field of the signed event (3.2). Identity answers who acted; execution context answers through which path; together they are the provenance the system verifies. An orchestrator's action is attributable and reconstructable on the same terms as a worker's or a human's, under one contract (2.2.7).

Third, the separation between orchestrator and worker is itself a control. They hold separate context windows, separate trust zones, and context does not cross: the orchestrator does not see the worker's internal reasoning or raw inputs, and the worker does not see the orchestrator's full context. That isolation is a privacy and blast-radius boundary; a compromise on one side does not surrender the other side's context, and sensitive data a worker handles need not flow back to the orchestrator. The isolation is structural; the privacy is only as good as the data minimization at the interface, scoped tasks in and results rather than raw data out, the same discipline as the input hash in 3.2. The two agents meet at exactly one place by design: a shared interface for authentication and execution. That shared layer is the common trust root, a common identity authority so every agent's actions are attributable, and a common enforcement point so every tool call from either agent is one signed action-event under one policy. The rule is: **isolate context, unify identity and enforcement.** Separate what each agent knows; share who they are and what they may do. Each orchestrator-to-worker handoff is an agent-to-agent edge (3.8), verified like any other event.

### 3.6 Crosscutting Concerns

**3.6.1 Identity and PKI.** Every actor, human, agent, service, holds a cryptographic identity from a common issuing authority. Agent identities are scoped to the tools and resource classes they may touch, and they grant nothing by default. This is the SPIFFE/SPIRE [10] workload-identity model applied to human and agent actors alike, the approach NIST's 2026 agent-identity work and industry efforts are converging on as the baseline.

**3.6.2 Single-use action tokens.** For an agent to act, a human or a deterministic gate authorizes one specific action, which mints a short-lived, scoped, single-use token the agent redeems exactly once and cannot replay. The agent keeps moving, but every action it takes was individually granted. Capability binds to the action, not the actor.

**3.6.3 Blast-radius separation of tools.** Non-destructive capability (reporting, notifying, ticketing) and destructive capability (cutting access, isolating hosts, moving money) run under separate identities and, ideally, separate MCP servers. A compromised reporting path physically cannot enforce. Destructive servers ship recommend-only by default, with automated action opt-in per resource class.

**3.6.4 Verifiable observability.** Tracing and metrics (for example, OpenTelemetry [13]) are necessary, but the authoritative record is the verifiable log, not the trace. Where traces leave the trust boundary (for example, a hosted tracing service), that egress is a deliberate decision, not a default.

**3.6.5 Event verification at ingest.** Before any workflow runs, the triggering event's signature is verified, and its freshness is checked against a nonce or monotonic sequence, so a validly signed event cannot be captured and replayed. The system acts only on events cryptographically confirmed to originate from a known producer and not seen before. Together these defeat event injection and replay.

### 3.7 Operational Overview (the provable loop)

The loop has five steps. Each produces a signed event, and each event binds the one before it, so a completed run is the hash-linked action chain of 3.4.2.

**Step 1: Sense.** A signed event arrives on the substrate. The control plane verifies its signature and freshness, and recomputes its replay hash, before anything else. An unverifiable event is rejected and logged. This is the only thing the system trusts to start work.

**Step 2: Reason.** The relevant agent (a node in the graph) reasons over the signed event as its context. If it needs more, it pulls signed evidence from the PIP. It produces a **proposal**, not a decision: a classification and a recommended action.

**Step 3: Decide.** The deterministic PDP takes the proposal plus signed context and returns allow or deny with a signed decision token. High blast-radius decisions route to a human-in-the-loop interrupt for approval. The model's proposal has now become a deterministic, signed decision.

**Step 4: Act.** The PEP verifies identity scope, the signed decision token, and the gate stack, then routes the action to a path that can prove it. For an agent, that means a single-use token against a scoped MCP tool. For a human, it means the audited path rather than an unaudited one.

**Step 5: Prove.** The action emits a signed result event, bound to the chain of steps that produced it and appended to the verifiable log with an inclusion proof. The loop's output is not just an effect on the world; it is new, verifiable evidence, which can trigger the next iteration. Every action becomes proof.

### 3.8 Applying It to Agent Frameworks

**LangChain and LangGraph.** Author the agent as a graph, not a free-roaming ReAct loop, one graph per event type. The reasoning nodes hold the model; the decision and enforcement nodes are deterministic. Make each edge a deterministic transition and have each node emit a signed event, so the graph's execution *is* the action chain of 3.4.2. LangGraph's checkpointing gives an in-flight workflow durability, and its interrupt mechanism is the natural home for the human-in-the-loop approval gate on destructive actions.

**MCP.** Model Context Protocol tool calls are the agent's actions on the world, so each tool call is a signed action-event subject to the PEP. Split tools across MCP servers by blast radius and scope each server's identity to its resource classes.

**A2A.** Agent-to-agent messages ride the substrate as signed events. One agent cannot drive another on an unverifiable message; A2A handoffs are verified like any other event, which prevents an injected or impersonated agent message from propagating through the chain. One honest limit: A2A is often cross-organizational, where the two agents do not share the common issuing authority this architecture otherwise assumes (2.4). Across that boundary, verification requires federated identity or a cross-domain trust bridge between the two authorities.

**The substrate.** Kafka or an equivalent carries the signed events and provides replay. Because the events are signed facts, the substrate carries durable, replayable evidence rather than a queue that forgets. The tamper-evidence is supplied by the verifiable log (3.4), not by the broker, which can still be compacted or pruned.

### 3.9 Coverage and Scope

This is a runtime action-path architecture. It is strongest exactly where an autonomous system's action meets trust and proof, and it is deliberately bounded. Stating the boundary is part of the design.

**Directly addressed.** Prompt injection reaching action is both prevented, at the prompt boundary (3.5.3), and contained, by the propose-decide-act split (3.5.1). Insecure or unsafe tool use is constrained by declared, scoped, single-use tool grants the model cannot expand (3.5, 3.6.2, 3.6.3). Non-repudiation, attribution, and audit are provided by the signed action chain and the verifiable log (3.2, 3.4). Unreliable model output is caught by the deterministic decision and verification path (2.2.3).

**Partially addressed: the architecture provides the substrate, not the active control.** Three classes of fault are guarded against but not solved here, and in each the architecture's contribution is the signed, ordered, verifiable event stream the solving layer consumes. Data exfiltration through the *action* path is contained by scoping and blast-radius separation, but exfiltration through the model's *output channel*, secrets encoded into a response, is not; output-side data-loss controls are a complementary layer. Runtime monitoring and detection of anomalous queries or unsafe outputs are not implemented here, but the signed event stream is close to an ideal input for them. API-level protections on the inference endpoint, authentication, rate limiting, quota, sit in front of this architecture, not inside it. Runaway cost is only partly bounded here: rejecting a failed proposal bounds the action path, but the inference and compute cost of a model that loops on repeatedly rejected proposals is bounded by those front-of-architecture controls and by explicit loop and retry budgets, not by determinism alone.

**Out of scope.** The architecture secures the runtime action path, not the model supply chain. Training and fine-tuning data integrity, model provenance and versioning, and hardening the model itself against jailbreak or denial-of-service are separate problems with their own controls. Nothing here makes a model harder to fool; it makes a fooled model unable to act without a deterministic decision and an indelible record.

The honest summary: this architecture makes the agent's actions verifiable and its decisions defensible. It assumes, and is designed to compose with, complementary layers for model hardening, output-channel data loss, and active monitoring.

---

## 4. Threat Model

The architecture is designed against the failure modes that ephemeral-message agent systems ignore. Each threat below names the mechanism that addresses it.

- **Event injection and replay.** Defeated by verifying every event's signature and replay hash at ingest. An attacker who cannot mint a valid signed event cannot drive the agent, and a captured event cannot be replayed.
- **Prompt injection.** Prevented at the prompt boundary and contained beyond it. Prevented because the prompt is built from trusted-source instructions while untrusted content enters as data that cannot become instruction (3.5.3); injected text is refused as a trust-zone violation at construction. Contained because even a proposal that slips through is only a proposal: it still must pass a deterministic PDP and a scoped PEP, neither of which the model controls.
- **Over-permissioned agents.** Contained by single-use, per-action tokens and blast-radius separation. There is no standing write power to steal.
- **Log tampering and history rewrite.** Defeated by consistency proofs. An operator who rebuilds the log to drop or alter a past event cannot produce a consistency proof against tree heads auditors already hold; the rewrite is detectable by any party, including one that trusts the operator not at all.
- **Equivocation (split-view).** A log presenting one history to one verifier and a different history to another is defeated by witnessing: independent parties gossip signed tree heads and the divergence is caught on comparison.
- **Non-repudiation and forensics.** Provided by the signed action chain and the verifiable log. Every action is attributable to an identity, tied to the decision and policy that authorized it, ordered within its chain, and reconstructable from signed evidence.
- **Insider misuse through unaudited paths.** Addressed by enforcement that guarantees provenance: privileged actions are routed to the path that produces a signed record, and the unaudited path is refused not because the actor lacks permission but because it cannot prove what happened.

Four classes of residual risk are worth stating plainly. First, enforcement in the action path is itself a high-value target and a potential single point of failure; the mitigations are blast-radius separation, recommend-only defaults, a global kill switch, and fail-safe-to-deny behavior, and enforcement is adopted last and gradually. Second, the control plane's own trust root is load-bearing: a deterministic PDP fed poisoned signed inputs produces a wrong decision just as deterministically, so determinism is not correctness. This shifts trust onto the issuing authority's keys and the policy itself, which must be protected by key custody, signed and versioned policy bundles with their own provenance, and reproducible PDP and PEP builds. Third, the human fallback degrades under load: if reviewers rubber-stamp the human-in-the-loop approvals, the determinism guarantee's safety net collapses into non-deterministic authorization wearing a deterministic mask, so approvals must be rate-limited, paced against fatigue, and themselves recorded as signed events. Fourth, this architecture deliberately does not address model hardening, output-channel data loss, or active runtime monitoring (3.9); it is designed to compose with those layers, and it feeds them the signed event stream they need.

---

## 5. Implementation Guidance

The architecture is meant to be retrofit incrementally, not adopted in a big bang.

1. **Start with verification, not enforcement.** Begin by signing events and verifying them at ingest, and by writing every event to an append-only log. This alone converts an ephemeral agent system into an auditable one and is low-risk.
2. **Make the model propose, not decide.** Refactor any place an LLM output directly authorizes an action so that the output is a proposal feeding a deterministic rule. This is the single highest-value change.
3. **Introduce the PDP and signed decisions.** Express your action policies as deterministic rules and have the PDP emit signed decision tokens. Decisions become reproducible and defensible.
4. **Scope identities and tools.** Issue per-agent identities, split tools by blast radius, and grant nothing by default.
5. **Add enforcement last, recommend-only first.** Turn on the PEP in a recommend-only posture, observe, then enable automated action per resource class, reversible actions before destructive ones, with human-in-the-loop on anything dangerous.

The order matters. Verification and provenance deliver value on their own and carry little risk. Enforcement delivers the most power and the most risk, so it comes last and is tightened slowly.

---

## 6. Conclusion

Event-driven architecture gave AI agents a nervous system: the ability to sense and act in real time, asynchronously, at scale. What it has not given them is a memory anyone can trust or a record anyone can audit. The events that drive the most autonomous systems we have built are still treated as ephemeral messages.

The fix is not a better model. It is a different treatment of the event. Make every event a signed, replayable fact. Keep the decision and verification path deterministic, with the model proposing and never deciding. Build the prompt from trusted sources so the context window stops being an implicit trust zone. Bind capability to a single approved action. Chain every step and record it as proof in a tamper-proof log. Apply this to both the humans and the agents, under one contract.

The result is autonomy you can verify: an agent system whose every action can be reconstructed, attributed, and defended by a party that trusts none of the actors. The industry agreed that agents should be event-driven. The next requirement is that the events be provable. Do it where it can be proven.

*A working reference implementation demonstrates the structural core of this design: trusted-source prompt construction, a model that proposes while a deterministic check decides, and action chains recorded as ordered step sequences. The verifiable log and the identity authority (3.4, 3.6.1) are mature, consumed components (RFC 6962 / Rekor; SPIFFE/SPIRE), not parts to be invented; wiring the signing and the append onto the action path is integration, and is the remaining step to a fully signed chain.*

---

## References

[1] S. Rose, O. Borchert, S. Mitchell, and S. Connelly, "Zero Trust Architecture," NIST SP 800-207, Aug. 2020. https://doi.org/10.6028/NIST.SP.800-207

[2] Joint Task Force, "Security and Privacy Controls for Information Systems and Organizations," NIST SP 800-53 Rev. 5, Sep. 2020. https://doi.org/10.6028/NIST.SP.800-53r5

[3] B. Laurie, A. Langley, and E. Kasper, "Certificate Transparency," RFC 6962, IETF, Jun. 2013. https://www.rfc-editor.org/rfc/rfc6962

[4] S. Torres-Arias, H. Afzali, T. K. Kuppusamy, R. Curtmola, and J. Cappos, "in-toto: Providing farm-to-table guarantees for bits and bytes," in *Proc. USENIX Security Symp.*, 2019. https://www.usenix.org/conference/usenixsecurity19/presentation/torres-arias

[5] Open Source Security Foundation, "SLSA: Supply-chain Levels for Software Artifacts." https://slsa.dev

[6] Sigstore project, "Rekor transparency log." https://www.sigstore.dev

[7] S. A. Crosby and D. S. Wallach, "Efficient Data Structures for Tamper-Evident Logging," in *Proc. USENIX Security Symp.*, 2009. https://www.usenix.org/conference/usenixsecurity09/technical-sessions/presentation/efficient-data-structures-tamper-evident

[8] E. Debenedetti, I. Shumailov, T. Fan, J. Hayes, N. Carlini, et al., "Defeating Prompt Injections by Design (CaMeL)," arXiv:2503.18813, 2025. https://arxiv.org/abs/2503.18813

[9] S. Willison, "The dual LLM pattern for building AI assistants that can resist prompt injection," 2025. https://simonwillison.net/2025/Apr/11/camel/

[10] Cloud Native Computing Foundation, "SPIFFE/SPIRE: Secure Production Identity Framework for Everyone." https://spiffe.io

[11] Cloud Native Computing Foundation, "Open Policy Agent (OPA)." https://www.openpolicyagent.org

[12] Amazon Web Services, "Cedar policy language." https://www.cedarpolicy.com

[13] Cloud Native Computing Foundation, "OpenTelemetry." https://opentelemetry.io

[14] Apache Software Foundation, "Apache Kafka." https://kafka.apache.org

[15] Anthropic, "Model Context Protocol (MCP)." https://modelcontextprotocol.io

[16] Linux Foundation, "Agent2Agent (A2A) Protocol." https://a2a-protocol.org

*Agent-specific 2025-2026 work named in 1.5, Microsoft's FIDES information-flow control for agents, hash-chained agent audit trails in emerging agent runtimes, the Cloud Security Alliance Agentic Trust Framework, NIST NCCoE work on AI agent identity, the OWASP Top 10 for Agentic Applications, and AIMS, is cited by name; confirm exact source links against the issuing bodies before publication.*
