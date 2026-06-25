package instance

import (
	"os"
	"path/filepath"
	"strings"
	"testing"
)

// Ports SelfTest.PathGuard: a normal relative path resolves under root; '..' and absolute paths reject.
func TestPathGuard(t *testing.T) {
	root, err := filepath.Abs(t.TempDir())
	if err != nil {
		t.Fatal(err)
	}
	i := &Instance{Root: root}

	ok, err := i.Resolve("sub/file.txt")
	if err != nil {
		t.Fatalf("relative path should resolve: %v", err)
	}
	if !strings.HasPrefix(strings.ToLower(ok), strings.ToLower(root)) {
		t.Fatalf("resolved path %q not under root %q", ok, root)
	}

	if _, err := i.Resolve("../escape.txt"); err == nil {
		t.Fatal("'..' should be rejected")
	}
	if _, err := i.Resolve(`C:\Windows\System32`); err == nil {
		t.Fatal("Windows-absolute path should be rejected (even on Linux)")
	}
	if _, err := i.Resolve("/etc/passwd"); err == nil {
		t.Fatal("absolute path should be rejected")
	}
	if _, err := i.Resolve(""); err == nil {
		t.Fatal("empty path should be rejected")
	}
}

// Open a directory with no config: it should still open with a default config and no manifest.
func TestOpenDefaultConfig(t *testing.T) {
	dir := t.TempDir()
	inst, err := Open(dir)
	if err != nil {
		t.Fatalf("open bare dir: %v", err)
	}
	if inst.Config == nil || inst.Config.Name == "" {
		t.Fatal("default config not applied")
	}
	if inst.Manifest != nil {
		t.Fatal("no manifest expected")
	}
}

// Open via a ratchet.json file and confirm the manifest loads from the workdir.
func TestOpenWithConfigAndManifest(t *testing.T) {
	dir := t.TempDir()
	if err := os.WriteFile(filepath.Join(dir, "ratchet.json"),
		[]byte(`{"name":"demo","domain":"test"}`), 0o644); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(filepath.Join(dir, "manifest.json"),
		[]byte(`{"name":"demo","entries":[{"id":"a","title":"A","path":"a.md","summary":"sa"}]}`), 0o644); err != nil {
		t.Fatal(err)
	}
	inst, err := Open(dir)
	if err != nil {
		t.Fatalf("open: %v", err)
	}
	if inst.Config.Name != "demo" || inst.Config.Domain != "test" {
		t.Fatalf("config wrong: %+v", inst.Config)
	}
	if inst.Manifest == nil || len(inst.Manifest.Entries) != 1 || inst.Manifest.Entries[0].ID != "a" {
		t.Fatalf("manifest wrong: %+v", inst.Manifest)
	}
}
