// Package pathutil holds small path helpers shared by the config/registry loaders. The recurring
// need is "resolve a path that a config file named, which may be absolute or relative to that
// file's folder" - so a knowledge base or a base dir can live anywhere on disk. Port of
// src.bak/PathUtil.cs.
package pathutil

import "path/filepath"

// ResolveAgainst resolves p to an absolute path. If p is already rooted, it returns its absolute
// form; otherwise it resolves p relative to the FOLDER of declaringFile (the config that named it).
// Returns "" for an empty p (the C# null).
func ResolveAgainst(declaringFile, p string) string {
	if p == "" {
		return ""
	}
	if filepath.IsAbs(p) {
		abs, err := filepath.Abs(p)
		if err != nil {
			return filepath.Clean(p)
		}
		return abs
	}
	baseDir := "."
	if declaringFile != "" {
		if df, err := filepath.Abs(declaringFile); err == nil {
			if d := filepath.Dir(df); d != "" {
				baseDir = d
			}
		}
	}
	abs, err := filepath.Abs(filepath.Join(baseDir, p))
	if err != nil {
		return filepath.Clean(filepath.Join(baseDir, p))
	}
	return abs
}
