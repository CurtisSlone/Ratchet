package model

import "testing"

// Ports SelfTest.ManifestHelpers: groups, by-group, by-doc-type, catalog filtering.
func TestManifestHelpers(t *testing.T) {
	m := &Manifest{Entries: []Entry{
		{ID: "a", Group: "creational", DocType: "pattern", Summary: "sa"},
		{ID: "b", Group: "structural", DocType: "pattern", Summary: "sb"},
		{ID: "c", Group: "creational", DocType: "pattern", Summary: "sc"},
		{ID: "d", Group: "", DocType: "reference", Summary: "sd"},
	}}
	if len(m.Groups()) != 2 {
		t.Fatalf("groups: want 2, got %d", len(m.Groups()))
	}
	if len(m.ByGroup("creational")) != 2 {
		t.Fatalf("by-group creational: want 2, got %d", len(m.ByGroup("creational")))
	}
	if len(m.ByDocType("reference")) != 1 {
		t.Fatalf("by-doc-type reference: want 1, got %d", len(m.ByDocType("reference")))
	}
	cat := m.Catalog("creational", "")
	if !contains(cat, "a [creational]: sa") || !contains(cat, "c [creational]: sc") || contains(cat, "- b") {
		t.Fatalf("catalog filtered wrong:\n%s", cat)
	}
}

func TestManifestGetEntryAndGroupsSorted(t *testing.T) {
	m := &Manifest{Entries: []Entry{
		{ID: "x", Group: "zeta"}, {ID: "y", Group: "alpha"},
	}}
	if m.GetEntry("y") == nil || m.GetEntry("nope") != nil {
		t.Fatal("GetEntry lookup wrong")
	}
	g := m.Groups()
	if len(g) != 2 || g[0] != "alpha" || g[1] != "zeta" {
		t.Fatalf("groups not sorted: %v", g)
	}
}

func TestValidateResultToText(t *testing.T) {
	ok := ValidateResult{Ok: true, Table: "skills"}
	if ok.ToText(40) != "PASS - 'skills' is valid under its schema." {
		t.Fatalf("ok text wrong: %q", ok.ToText(40))
	}
	bad := ValidateResult{Table: "skills", Problems: []Problem{{Row: 1, Col: "id", Msg: "boom"}}}
	if !contains(bad.ToText(40), "[row 1] id: boom") {
		t.Fatalf("fail text wrong: %q", bad.ToText(40))
	}
}

func TestLoadKnowledgeList(t *testing.T) {
	arr := []any{
		map[string]any{"name": "kb", "path": "/x", "default": true},
		map[string]any{"name": "", "path": "/y"}, // dropped: no name
		map[string]any{"name": "z"},              // dropped: no path
	}
	list := LoadKnowledgeList(arr)
	if len(list) != 1 || list[0].Name != "kb" || !list[0].Default {
		t.Fatalf("knowledge list wrong: %+v", list)
	}
	var reg KnowledgeRegistry
	reg.Add("kb", "/abs", true)
	reg.Add("kb", "/abs2", false) // override by name
	if len(reg.Bases) != 1 || reg.Find("KB").Path != "/abs2" {
		t.Fatalf("registry add/override wrong: %+v", reg.Bases)
	}
	if len(reg.Defaults()) != 0 {
		t.Fatalf("defaults: want 0 after override to non-default")
	}
}

func contains(s, sub string) bool {
	return len(s) >= len(sub) && (indexOf(s, sub) >= 0)
}

func indexOf(s, sub string) int {
	for i := 0; i+len(sub) <= len(s); i++ {
		if s[i:i+len(sub)] == sub {
			return i
		}
	}
	return -1
}
