package snapshot

import (
	"os"
	"path/filepath"
	"testing"

	"github.com/scanset/Ratchet/internal/instance"
	"github.com/scanset/Ratchet/internal/runrec"
)

func write(t *testing.T, p, s string) {
	t.Helper()
	if err := os.MkdirAll(filepath.Dir(p), 0o755); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(p, []byte(s), 0o644); err != nil {
		t.Fatal(err)
	}
}

func read(t *testing.T, p string) string {
	t.Helper()
	b, err := os.ReadFile(p)
	if err != nil {
		t.Fatal(err)
	}
	return string(b)
}

func TestIgnored(t *testing.T) {
	for _, rel := range []string{"node_modules/x", "a/.git/c", "target/x", "vendor/bundle/g", "build/o", "tmp/t"} {
		if !ignored(rel) {
			t.Errorf("expected ignored: %s", rel)
		}
	}
	for _, rel := range []string{"main.go", "src/app.rb", "vendor/foo.go", "a/b.txt"} {
		if ignored(rel) {
			t.Errorf("expected NOT ignored: %s", rel)
		}
	}
}

func TestSnapshotRestoreRoundTrip(t *testing.T) {
	ws := t.TempDir()
	snap := filepath.Join(t.TempDir(), "snap")
	write(t, filepath.Join(ws, "a.txt"), "original")
	write(t, filepath.Join(ws, "sub", "b.txt"), "two")
	write(t, filepath.Join(ws, "node_modules", "huge.bin"), "IGNORED")

	if err := Snapshot(ws, snap); err != nil {
		t.Fatal(err)
	}
	if _, err := os.Stat(filepath.Join(snap, "node_modules", "huge.bin")); err == nil {
		t.Fatal("node_modules should not be snapshotted")
	}

	write(t, filepath.Join(ws, "a.txt"), "CHANGED")
	write(t, filepath.Join(ws, "c.txt"), "new file")
	if err := os.Remove(filepath.Join(ws, "sub", "b.txt")); err != nil {
		t.Fatal(err)
	}

	if err := Restore(snap, ws); err != nil {
		t.Fatal(err)
	}
	if got := read(t, filepath.Join(ws, "a.txt")); got != "original" {
		t.Errorf("a.txt = %q, want original", got)
	}
	if got := read(t, filepath.Join(ws, "sub", "b.txt")); got != "two" {
		t.Errorf("b.txt not restored: %q", got)
	}
	if _, err := os.Stat(filepath.Join(ws, "c.txt")); err == nil {
		t.Error("c.txt should be removed by an exact restore")
	}
	if got := read(t, filepath.Join(ws, "node_modules", "huge.bin")); got != "IGNORED" {
		t.Errorf("node_modules clobbered by restore: %q", got)
	}
}

func TestDiff(t *testing.T) {
	before, after := t.TempDir(), t.TempDir()
	write(t, filepath.Join(before, "same.txt"), "s")
	write(t, filepath.Join(after, "same.txt"), "s")
	write(t, filepath.Join(before, "gone.txt"), "x")
	write(t, filepath.Join(after, "new.txt"), "n")
	write(t, filepath.Join(before, "mod.txt"), "v1")
	write(t, filepath.Join(after, "mod.txt"), "v2")
	write(t, filepath.Join(before, "node_modules", "x"), "i1")
	write(t, filepath.Join(after, "node_modules", "x"), "i2")

	ch, err := Diff(before, after)
	if err != nil {
		t.Fatal(err)
	}
	got := map[string]string{}
	for _, c := range ch {
		got[c.Path] = c.Status
	}
	if got["new.txt"] != runrec.Added {
		t.Errorf("new.txt status = %q", got["new.txt"])
	}
	if got["gone.txt"] != runrec.Deleted {
		t.Errorf("gone.txt status = %q", got["gone.txt"])
	}
	if got["mod.txt"] != runrec.Modified {
		t.Errorf("mod.txt status = %q", got["mod.txt"])
	}
	if _, ok := got["same.txt"]; ok {
		t.Error("same.txt should not be a change")
	}
	if _, ok := got["node_modules/x"]; ok {
		t.Error("ignored paths should not appear in the diff")
	}
}

func TestRollbackIntegration(t *testing.T) {
	inst, err := instance.Open(t.TempDir())
	if err != nil {
		t.Fatal(err)
	}
	wsName := "proj"
	wsAbs := filepath.Join(inst.WorkspacesDirAbs(), wsName)
	write(t, filepath.Join(wsAbs, "main.go"), "package v1")

	id, err := RecordPoint(inst, runrec.KindSnapshot, wsAbs, wsName, "", "t", "test", "cli")
	if err != nil {
		t.Fatal(err)
	}
	if !Exists(inst, id) {
		t.Fatal("snapshot should exist after RecordPoint")
	}

	write(t, filepath.Join(wsAbs, "main.go"), "package v2")
	newID, changed, err := RollbackTo(inst, wsAbs, wsName, id, "t", "test", "cli", 10)
	if err != nil {
		t.Fatal(err)
	}
	if changed != 1 {
		t.Errorf("expected 1 changed file, got %d", changed)
	}
	if got := read(t, filepath.Join(wsAbs, "main.go")); got != "package v1" {
		t.Errorf("rollback did not restore: %q", got)
	}
	if !Exists(inst, newID) {
		t.Fatal("rollback run should have its own (reversible) snapshot")
	}
	idx, _ := runrec.ReadIndex(inst)
	if len(idx) != 2 {
		t.Fatalf("expected 2 index entries (snapshot + rollback), got %d", len(idx))
	}
}

func TestPrune(t *testing.T) {
	inst, err := instance.Open(t.TempDir())
	if err != nil {
		t.Fatal(err)
	}
	wsName := "p"
	wsAbs := filepath.Join(inst.WorkspacesDirAbs(), wsName)
	write(t, filepath.Join(wsAbs, "f.txt"), "x")
	var ids []string
	for i := 0; i < 5; i++ {
		id, err := RecordPoint(inst, runrec.KindSnapshot, wsAbs, wsName, "", "t", "test", "cli")
		if err != nil {
			t.Fatal(err)
		}
		ids = append(ids, id)
	}
	if err := Prune(inst, wsName, 2); err != nil {
		t.Fatal(err)
	}
	kept := 0
	for _, id := range ids {
		if Exists(inst, id) {
			kept++
		}
	}
	if kept != 2 {
		t.Errorf("expected 2 snapshots kept after prune, got %d", kept)
	}
}
