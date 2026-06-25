// Tsv - one place for the tab-separated line handling the oracle, the dispatcher, and the propose
// flow share: CRLF-tolerant splitting into non-empty lines (and into tab-split rows). Port of
// src.bak/Runtime/Tsv.cs.
package oracle

import "strings"

// NonEmptyLines returns the non-empty lines, with a trailing '\r' stripped (tolerate Windows CRLF).
func NonEmptyLines(text string) []string {
	var out []string
	for _, raw := range strings.Split(text, "\n") {
		line := strings.TrimRight(raw, "\r")
		if strings.TrimSpace(line) != "" {
			out = append(out, line)
		}
	}
	return out
}

// Rows returns the non-empty lines, each split into cells on tabs.
func Rows(text string) [][]string {
	var out [][]string
	for _, line := range NonEmptyLines(text) {
		out = append(out, strings.Split(line, "\t"))
	}
	return out
}
