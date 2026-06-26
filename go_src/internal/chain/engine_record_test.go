package chain

import (
	"os"
	"path/filepath"
	"testing"

	"github.com/scanset/Ratchet/internal/conventions"
	"github.com/scanset/Ratchet/internal/instance"
	"github.com/scanset/Ratchet/internal/model"
	"github.com/scanset/Ratchet/internal/runrec"
	"github.com/scanset/Ratchet/internal/snapshot"
)

type fakeGen struct{}

func (fakeGen) URL() string                              { return "http://test" }
func (fakeGen) Generate(string, float64) (string, error) { return "out", nil }

// A workspace-targeting run must snapshot the workspace and write the full record set.
func TestRunWritesRecordsAndSnapshot(t *testing.T) {
	inst, err := instance.Open(t.TempDir())
	if err != nil {
		t.Fatal(err)
	}
	wsAbs := filepath.Join(inst.WorkspacesDirAbs(), "proj")
	if err := os.MkdirAll(wsAbs, 0o755); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(filepath.Join(wsAbs, "x.txt"), []byte("hi"), 0o644); err != nil {
		t.Fatal(err)
	}

	c := &model.Chain{ID: "t", Entry: "end", Actions: map[string]model.ActionNode{
		"end": {ID: "end", Kind: conventions.ActionKindExit, Outcome: "success"},
	}}
	res := NewEngine(inst, fakeGen{}, nil).WithCaller("cli").Run(c, "the input", wsAbs)
	if res.IsError {
		t.Fatalf("run errored: %s", res.Outcome)
	}

	idx, _ := runrec.ReadIndex(inst)
	if len(idx) != 1 {
		t.Fatalf("expected 1 index entry, got %d", len(idx))
	}
	id := idx[0].RunID
	if idx[0].Workspace != "proj" || !idx[0].Rollbackable {
		t.Errorf("index entry = %+v", idx[0])
	}
	for _, f := range []string{"meta.json", "step-001.json", "outcome.json", "changes.json"} {
		if _, err := os.Stat(filepath.Join(inst.Root, "runs", id, f)); err != nil {
			t.Errorf("missing run record %s: %v", f, err)
		}
	}
	if !snapshot.Exists(inst, id) {
		t.Error("expected a before-snapshot for a workspace run")
	}
}

// A run with no workspace records everything except a snapshot (nothing to roll back).
func TestRunNoWorkspaceNoSnapshot(t *testing.T) {
	inst, err := instance.Open(t.TempDir())
	if err != nil {
		t.Fatal(err)
	}
	c := &model.Chain{ID: "t", Entry: "end", Actions: map[string]model.ActionNode{
		"end": {ID: "end", Kind: conventions.ActionKindExit, Outcome: "success"},
	}}
	res := NewEngine(inst, fakeGen{}, nil).Run(c, "in", "")
	if res.IsError {
		t.Fatalf("run errored: %s", res.Outcome)
	}
	idx, _ := runrec.ReadIndex(inst)
	if len(idx) != 1 || idx[0].Rollbackable {
		t.Fatalf("no-workspace run should not be rollbackable: %+v", idx)
	}
}
