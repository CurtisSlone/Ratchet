// Package markdown is a tiny, dependency-free markdown parser. It does NOT render; it turns markdown
// text into a flat list of typed lines (each with inline spans) a caller can map to its own
// formatting (the console uses StripFence to unwrap code fences). Supported: fenced/indented code,
// headings, unordered/ordered lists, blockquotes, horizontal rules, and inline code/bold/italic/
// links. A pragmatic subset, not CommonMark. Port of src.bak/Runtime/Markdown.cs.
package markdown

import "strings"

// LineKind classifies a parsed line.
type LineKind int

const (
	Blank LineKind = iota
	Paragraph
	Heading
	Bullet
	Ordered
	Code
	Rule
	Quote
)

// SpanStyle classifies an inline span.
type SpanStyle int

const (
	Plain SpanStyle = iota
	Bold
	Italic
	CodeSpan
	Link
)

// Span is one inline run.
type Span struct {
	Text  string
	Style SpanStyle
	Href  string // for Link
}

// Line is one parsed block line.
type Line struct {
	Kind   LineKind
	Level  int    // heading level (1-6), or list indent depth
	Marker string // ordered-list number text, e.g. "2"
	Raw    string // verbatim text for Code lines
	Spans  []Span
}

// Parse turns markdown into a flat list of typed lines.
func Parse(md string) []Line {
	var lines []Line
	raw := strings.Split(strings.ReplaceAll(strings.ReplaceAll(md, "\r\n", "\n"), "\r", "\n"), "\n")
	inFence := false

	for _, line := range raw {
		if strings.HasPrefix(strings.TrimLeft(line, " \t"), "```") {
			inFence = !inFence // the fence line itself is not emitted
			continue
		}
		if inFence {
			lines = append(lines, Line{Kind: Code, Raw: line})
			continue
		}

		s := line
		if strings.TrimSpace(s) == "" {
			lines = append(lines, Line{Kind: Blank})
			continue
		}

		if h := headingLevel(s); h > 0 {
			lines = append(lines, Line{Kind: Heading, Level: h, Spans: ParseInline(strings.TrimSpace(s[h:]))})
			continue
		}

		st := strings.TrimSpace(s)
		if st == "---" || st == "***" || st == "___" {
			lines = append(lines, Line{Kind: Rule})
			continue
		}
		if strings.HasPrefix(st, "> ") {
			lines = append(lines, Line{Kind: Quote, Spans: ParseInline(st[2:])})
			continue
		}

		if indent, rest, ok := tryBullet(s); ok {
			lines = append(lines, Line{Kind: Bullet, Level: indent, Spans: ParseInline(rest)})
			continue
		}
		if indent, marker, rest, ok := tryOrdered(s); ok {
			lines = append(lines, Line{Kind: Ordered, Level: indent, Marker: marker, Spans: ParseInline(rest)})
			continue
		}

		lines = append(lines, Line{Kind: Paragraph, Spans: ParseInline(strings.TrimRight(s, " \t"))})
	}
	return lines
}

// ParseInline splits a line into inline spans: `code`, **bold**, *italic* / _italic_, [text](url),
// plain.
func ParseInline(text string) []Span {
	var spans []Span
	if text == "" {
		return spans
	}
	var plain strings.Builder
	i, n := 0, len(text)
	for i < n {
		ch := text[i]

		if ch == '`' {
			if e := indexByteFrom(text, '`', i+1); e > i {
				flush(&spans, &plain)
				spans = append(spans, Span{Text: text[i+1 : e], Style: CodeSpan})
				i = e + 1
				continue
			}
		}
		if ch == '*' && i+1 < n && text[i+1] == '*' {
			if e := indexStrFrom(text, "**", i+2); e > i+1 {
				flush(&spans, &plain)
				spans = append(spans, Span{Text: text[i+2 : e], Style: Bold})
				i = e + 2
				continue
			}
		}
		if ch == '*' || ch == '_' {
			if e := indexByteFrom(text, ch, i+1); e > i+1 {
				flush(&spans, &plain)
				spans = append(spans, Span{Text: text[i+1 : e], Style: Italic})
				i = e + 1
				continue
			}
		}
		if ch == '[' {
			if c := indexByteFrom(text, ']', i+1); c > i && c+1 < n && text[c+1] == '(' {
				if p := indexByteFrom(text, ')', c+2); p > c {
					flush(&spans, &plain)
					spans = append(spans, Span{Text: text[i+1 : c], Style: Link, Href: text[c+2 : p]})
					i = p + 1
					continue
				}
			}
		}

		plain.WriteByte(ch)
		i++
	}
	flush(&spans, &plain)
	return spans
}

// StripFence: if text is a single fenced code block (```lang ... ```), return just the inner code;
// otherwise return it unchanged. Used when writing generated code to a file.
func StripFence(text string) string {
	if text == "" {
		return text
	}
	t := strings.TrimSpace(text)
	if !strings.HasPrefix(t, "```") {
		return text
	}
	firstNl := strings.IndexByte(t, '\n')
	if firstNl < 0 {
		return text
	}
	lastFence := strings.LastIndex(t, "```")
	if lastFence <= firstNl {
		return text
	}
	return strings.TrimRight(t[firstNl+1:lastFence], "\r\n")
}

func flush(spans *[]Span, plain *strings.Builder) {
	if plain.Len() > 0 {
		*spans = append(*spans, Span{Text: plain.String(), Style: Plain})
		plain.Reset()
	}
}

func headingLevel(s string) int {
	n := 0
	for n < len(s) && s[n] == '#' {
		n++
	}
	if n >= 1 && n <= 6 && n < len(s) && s[n] == ' ' {
		return n
	}
	return 0
}

func tryBullet(line string) (indent int, rest string, ok bool) {
	sp := 0
	for sp < len(line) && line[sp] == ' ' {
		sp++
	}
	r := line[sp:]
	if strings.HasPrefix(r, "- ") || strings.HasPrefix(r, "* ") || strings.HasPrefix(r, "+ ") {
		return sp / 2, r[2:], true
	}
	return 0, "", false
}

func tryOrdered(line string) (indent int, marker, rest string, ok bool) {
	sp := 0
	for sp < len(line) && line[sp] == ' ' {
		sp++
	}
	d := sp
	for d < len(line) && line[d] >= '0' && line[d] <= '9' {
		d++
	}
	if d > sp && d+1 < len(line) && line[d] == '.' && line[d+1] == ' ' {
		return sp / 2, line[sp:d], line[d+2:], true
	}
	return 0, "", "", false
}

// indexByteFrom returns the absolute index of c in s at or after from, or -1.
func indexByteFrom(s string, c byte, from int) int {
	if from > len(s) {
		return -1
	}
	r := strings.IndexByte(s[from:], c)
	if r < 0 {
		return -1
	}
	return from + r
}

// indexStrFrom returns the absolute index of sub in s at or after from, or -1.
func indexStrFrom(s, sub string, from int) int {
	if from > len(s) {
		return -1
	}
	r := strings.Index(s[from:], sub)
	if r < 0 {
		return -1
	}
	return from + r
}
