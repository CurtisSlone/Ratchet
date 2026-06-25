// Manifest is the routing index the dispatcher picks on (manifest.json). The summaries are the only
// thing routing sees; the host reads this to know what KB entries exist and where they live. Port of
// src.bak/Model/Manifest.cs.
package model

import (
	"encoding/json"
	"fmt"
	"os"
	"sort"
	"strings"
)

// Entry is one routable KB document's metadata. Field order matches the C# writer (id, title, path,
// summary, doc_type, group, keywords) so regenerated manifests are byte-stable.
type Entry struct {
	ID       string   `json:"id"`
	Title    string   `json:"title"`
	Path     string   `json:"path"`
	Summary  string   `json:"summary"`
	DocType  string   `json:"doc_type"`
	Group    string   `json:"group"` // sub-folder under the routable layer, e.g. "dotnet" or "creational"
	Keywords []string `json:"keywords"`
}

// Manifest is a named index of entries.
type Manifest struct {
	Name        string
	Description string
	Entries     []Entry
}

type manifestJSON struct {
	Name        string  `json:"name"`
	Description string  `json:"description"`
	Entries     []Entry `json:"entries"`
}

// LoadManifest reads and parses a manifest.json.
func LoadManifest(path string) (*Manifest, error) {
	data, err := os.ReadFile(path)
	if err != nil {
		return nil, fmt.Errorf("reading %s: %v", path, err)
	}
	var raw manifestJSON
	if err := json.Unmarshal(data, &raw); err != nil {
		return nil, fmt.Errorf("parsing %s: %v", path, err)
	}
	m := &Manifest{Name: raw.Name, Description: raw.Description}
	for _, e := range raw.Entries {
		if e.Keywords == nil {
			e.Keywords = []string{}
		}
		m.Entries = append(m.Entries, e)
	}
	return m, nil
}

// GetEntry returns the entry with the given id, or nil.
func (m *Manifest) GetEntry(id string) *Entry {
	for i := range m.Entries {
		if m.Entries[i].ID == id {
			return &m.Entries[i]
		}
	}
	return nil
}

// Groups returns the distinct, sorted sub-folder groups present in the index.
func (m *Manifest) Groups() []string {
	var seen []string
	known := map[string]bool{}
	for _, e := range m.Entries {
		if e.Group != "" && !known[e.Group] {
			known[e.Group] = true
			seen = append(seen, e.Group)
		}
	}
	sort.Slice(seen, func(i, j int) bool { return strings.ToLower(seen[i]) < strings.ToLower(seen[j]) })
	return seen
}

// ByGroup returns the entries in the given group (case-insensitive).
func (m *Manifest) ByGroup(group string) []Entry {
	var o []Entry
	for _, e := range m.Entries {
		if strings.EqualFold(e.Group, group) {
			o = append(o, e)
		}
	}
	return o
}

// ByDocType returns the entries of the given doc_type (case-insensitive).
func (m *Manifest) ByDocType(docType string) []Entry {
	var o []Entry
	for _, e := range m.Entries {
		if strings.EqualFold(e.DocType, docType) {
			o = append(o, e)
		}
	}
	return o
}

// Catalog renders a compact "id [group]: summary" listing, optionally filtered by group and/or
// doc_type. An empty filter means "no filter on that field". This is the catalog the model enumerates.
func (m *Manifest) Catalog(group, docType string) string {
	var sb strings.Builder
	for _, e := range m.Entries {
		if group != "" && !strings.EqualFold(e.Group, group) {
			continue
		}
		if docType != "" && !strings.EqualFold(e.DocType, docType) {
			continue
		}
		g := ""
		if e.Group != "" {
			g = " [" + e.Group + "]"
		}
		sb.WriteString("- " + e.ID + g + ": " + e.Summary + "\n")
	}
	return strings.TrimRight(sb.String(), "\n")
}
