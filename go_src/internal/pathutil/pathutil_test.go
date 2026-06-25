package pathutil

import (
	"path/filepath"
	"testing"
)

func TestResolveAgainstEmpty(t *testing.T) {
	if got := ResolveAgainst("/some/config.json", ""); got != "" {
		t.Fatalf("empty p: want \"\", got %q", got)
	}
}

func TestResolveAgainstRelative(t *testing.T) {
	// relative p resolves against the FOLDER of the declaring file
	got := ResolveAgainst("/home/user/ratchet/ratchet.json", "kb/docs")
	want, _ := filepath.Abs("/home/user/ratchet/kb/docs")
	if got != want {
		t.Fatalf("relative: want %q, got %q", want, got)
	}
}

func TestResolveAgainstAbsolute(t *testing.T) {
	abs := string(filepath.Separator) + filepath.Join("opt", "kb")
	got := ResolveAgainst("/home/user/ratchet/ratchet.json", abs)
	want, _ := filepath.Abs(abs)
	if got != want {
		t.Fatalf("absolute: want %q, got %q", want, got)
	}
}

func TestResolveAgainstNoDeclaringFile(t *testing.T) {
	got := ResolveAgainst("", "x/y")
	want, _ := filepath.Abs(filepath.Join(".", "x/y"))
	if got != want {
		t.Fatalf("no declaring file: want %q, got %q", want, got)
	}
}
