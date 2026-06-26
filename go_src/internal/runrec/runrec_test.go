package runrec

import (
	"os"
	"path/filepath"
	"testing"
	"time"

	"github.com/scanset/Ratchet/internal/instance"
)

func TestHashDeterministicAndSensitive(t *testing.T) {
	m := Meta{RunID: "r1", Kind: KindFlow, ChainID: "c", Input: "x"}
	h1 := HashMeta(m)
	if h1 != HashMeta(m) {
		t.Fatal("HashMeta not deterministic")
	}
	if len(h1) != 64 {
		t.Fatalf("sha256 hex want 64 chars, got %d", len(h1))
	}
	m2 := m
	m2.Input = "y"
	if HashMeta(m2) == h1 {
		t.Fatal("HashMeta insensitive to a field change")
	}
}

func TestStepHashChainBinding(t *testing.T) {
	s := Step{Index: 1, Node: "n", Kind: "generate", PrevHash: "AAAA"}
	h := s.computeHash()
	if s.computeHash() != h {
		t.Fatal("step hash not stable")
	}
	s2 := s
	s2.PrevHash = "BBBB"
	if s2.computeHash() == h {
		t.Fatal("prev_hash is not bound into the step hash (chain would be forgeable)")
	}
}

func TestRunIDFormat(t *testing.T) {
	id := RunID(time.Date(2026, 6, 26, 10, 14, 55, 450*int(time.Millisecond), time.UTC))
	if id != "20260626-101455-450" {
		t.Fatalf("RunID = %q, want 20260626-101455-450", id)
	}
}

func TestIORoundTripAndUniqueID(t *testing.T) {
	inst, err := instance.Open(t.TempDir())
	if err != nil {
		t.Fatal(err)
	}
	now := time.Now()
	id := UniqueRunID(inst, now)
	m := Meta{RunID: id, Kind: KindFlow, ChainID: "c", Workspace: "proj"}
	prev, err := WriteMeta(inst, m)
	if err != nil || len(prev) != 64 {
		t.Fatalf("WriteMeta: %v hash=%q", err, prev)
	}
	prev, err = WriteStep(inst, id, Step{Index: 1, Node: "n", Kind: "generate"}, prev)
	if err != nil {
		t.Fatal(err)
	}
	if err := WriteOutcome(inst, id, Outcome{Outcome: "ok", Steps: 1}, prev); err != nil {
		t.Fatal(err)
	}
	if err := AppendIndex(inst, IndexEntry{RunID: id, Workspace: "proj", Outcome: "ok", Rollbackable: true}); err != nil {
		t.Fatal(err)
	}
	idx, err := ReadIndex(inst)
	if err != nil || len(idx) != 1 || idx[0].RunID != id {
		t.Fatalf("ReadIndex = %+v err=%v", idx, err)
	}
	if _, err := os.Stat(filepath.Join(inst.Root, "runs", id, "meta.json")); err != nil {
		t.Fatalf("meta.json not written: %v", err)
	}
	// UniqueRunID must avoid the now-existing run dir.
	if UniqueRunID(inst, now) == id {
		t.Fatal("UniqueRunID returned a colliding id")
	}
}
