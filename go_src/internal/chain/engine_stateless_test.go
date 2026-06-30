package chain

import (
	"strings"
	"testing"

	"github.com/scanset/Ratchet/internal/instance"
	"github.com/scanset/Ratchet/internal/model"
)

// Statelessness guarantee (see docs/concepts/context-binding.md#stateless-by-construction): a node sees
// ONLY its declared bindings. A later node cannot observe an earlier node's output unless it explicitly
// binds it - even though that output sits on the run's data bus. resolveSlots is the chokepoint that
// enforces this, so we assert it directly: if this test ever fails, flows have stopped being stateless.
func TestStatelessNodeSeesOnlyBoundInputs(t *testing.T) {
	inst, err := instance.Open(t.TempDir())
	if err != nil {
		t.Fatal(err)
	}
	e := NewEngine(inst, fakeGen{}, nil)

	const secret = "SECRET_OUTPUT_OF_NODE1"
	// node1 has already run; its output is on the data bus (state) under its node id.
	state := map[string]string{
		"$input":     "the task",
		"$workspace": "",
		"node1":      secret,
	}

	// node2 binds ONLY $input - it does NOT bind node1.
	node2 := model.ActionNode{ID: "node2", Inputs: []model.InputBinding{
		{Source: "from", From: "$input", Path: ".", As: "task"},
	}}
	slots := e.resolveSlots(node2, state)
	for k, v := range slots {
		if strings.Contains(v, secret) {
			t.Fatalf("statelessness violated: node2 slot %q leaked node1's output without binding it: %q", k, v)
		}
	}
	if slots["task"] != "the task" {
		t.Fatalf("node2 should see its bound $input, got %q", slots["task"])
	}

	// Positive control: binding is what grants visibility. A node that DOES bind node1 sees it.
	node2b := model.ActionNode{ID: "node2b", Inputs: []model.InputBinding{
		{Source: "from", From: "node1", Path: ".", As: "prev"},
	}}
	if got := e.resolveSlots(node2b, state)["prev"]; got != secret {
		t.Fatalf("explicit binding {from: node1} should surface node1's output, got %q", got)
	}
}
