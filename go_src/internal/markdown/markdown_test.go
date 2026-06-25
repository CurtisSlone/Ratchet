package markdown

import (
	"strings"
	"testing"
)

// Ports SelfTest.MarkdownParse.
func TestParseInlineAndBlocks(t *testing.T) {
	// inline: plain + code + bold spans in order
	sp := ParseInline("use `csc` and **flags**")
	if len(sp) != 4 {
		t.Fatalf("inline spans: want 4, got %d (%+v)", len(sp), sp)
	}
	if sp[0].Style != Plain || sp[1].Style != CodeSpan || sp[1].Text != "csc" {
		t.Fatalf("inline first spans wrong: %+v", sp)
	}
	if sp[3].Style != Bold || sp[3].Text != "flags" {
		t.Fatalf("inline bold span wrong: %+v", sp[3])
	}

	// a link span carries its href
	ln := ParseInline("see [docs](http://x)")
	if ln[1].Style != Link || ln[1].Text != "docs" || ln[1].Href != "http://x" {
		t.Fatalf("link span wrong: %+v", ln[1])
	}

	// block kinds: heading, fenced code (fence lines not emitted), bullet
	doc := Parse("# Title\n```\ncode line\n```\n- item one")
	if doc[0].Kind != Heading || doc[0].Level != 1 {
		t.Fatalf("heading wrong: %+v", doc[0])
	}
	var hasCode, hasBullet, hasFenceText bool
	for _, l := range doc {
		if l.Kind == Code {
			hasCode = true
			if l.Raw != "code line" {
				t.Fatalf("code raw wrong: %q", l.Raw)
			}
		}
		if l.Kind == Bullet {
			hasBullet = true
		}
		if l.Kind == Paragraph && len(l.Spans) > 0 && strings.Contains(l.Spans[0].Text, "```") {
			hasFenceText = true
		}
	}
	if !hasCode || !hasBullet || hasFenceText {
		t.Fatalf("block parse wrong: code=%v bullet=%v fenceText=%v", hasCode, hasBullet, hasFenceText)
	}
}

// Ports the StripFence half of SelfTest.SlashRedirect.
func TestStripFence(t *testing.T) {
	if got := StripFence("```csharp\nint x = 1;\n```"); got != "int x = 1;" {
		t.Fatalf("strip fence wrong: %q", got)
	}
	if got := StripFence("plain text"); got != "plain text" {
		t.Fatalf("non-fenced should pass through: %q", got)
	}
}
