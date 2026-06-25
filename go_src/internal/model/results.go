// Results - the small structured outcome types passed between the runtime and the front ends.
// Keeping these as data lets callers branch on Ok instead of sniffing PASS/FAIL text. Port of
// src.bak/Model/Results.cs.
package model

import (
	"fmt"
	"strings"
)

// TurnResult is the outcome of one dispatcher turn. Front ends render it however they like.
type TurnResult struct {
	Intent        string // ask | make | validate | propose | help | quit | (unknown)
	Query         string // the core query the dispatcher extracted
	Standalone    string // the utterance after coreference rewrite (== input if none)
	Rewritten     bool   // true when Standalone differs from the raw input
	Text          string // the capability's output to show the user
	IsError       bool   // true when Text is an error message
	ProposedTable string // set on a successful propose: the target table
	ProposedRow   string // set on a successful propose: the validated tab-joined row
	WrittenPath   string // set when output was redirected to a file ("> path"); absolute path
	Streamed      bool   // true when Text was already streamed to the front end via a token sink
}

// ValidateResult is the outcome of an oracle run on a table.
type ValidateResult struct {
	Ok       bool
	Table    string
	Problems []Problem
}

// ToText renders the result (capped). Callers branch on Ok, not on this text.
func (v ValidateResult) ToText(maxShown int) string {
	if v.Ok {
		return "PASS - '" + v.Table + "' is valid under its schema."
	}
	var sb strings.Builder
	sb.WriteString(fmt.Sprintf("FAIL - %d problem(s) in '%s':\n", len(v.Problems), v.Table))
	for i, p := range v.Problems {
		if i >= maxShown {
			break
		}
		sb.WriteString("  " + p.String() + "\n")
	}
	return sb.String()
}

// ProposeResult is the outcome of a propose-row run: a model proposal gated by the oracle, with
// bounded repair.
type ProposeResult struct {
	Ok       bool
	Table    string
	Header   string // tab-joined header used for validation
	Row      string // the best/last tab-joined row produced
	Attempts int
	Error    string // non-empty on a hard failure (load/model error)
	Problems []Problem
}

// ToolRunResult is the outcome of running an instance command/script tool.
type ToolRunResult struct {
	Ok       bool
	ExitCode int
	TimedOut bool
	Stdout   string
	Stderr   string
	Output   string // combined, human/agent-facing
	Error    string // non-empty on a host-side failure (couldn't launch, etc.)
}

// FlowInfo is lightweight workflow metadata for the conversational router's catalog (no chain loaded).
type FlowInfo struct {
	ID        string // the chain dir name - how `/flow <id>` refers to it
	Name      string //
	WhenToUse string // the router's match surface (a chain's summary)
}
