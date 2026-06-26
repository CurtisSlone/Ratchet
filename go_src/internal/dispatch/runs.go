package dispatch

import (
	"fmt"
	"path/filepath"
	"sort"
	"strings"

	"github.com/scanset/Ratchet/internal/model"
	"github.com/scanset/Ratchet/internal/runrec"
	"github.com/scanset/Ratchet/internal/snapshot"
	"github.com/scanset/Ratchet/internal/version"
)

const (
	defaultRunsList = 15
	snapshotKeep    = 10
)

// doRuns lists recent runs from the index, newest first (the audit/observability log).
func (d *Dispatcher) doRuns(rest string, r *model.TurnResult) {
	idx, _ := runrec.ReadIndex(d.inst)
	if len(idx) == 0 {
		r.Text = "No runs recorded yet."
		return
	}
	sort.Slice(idx, func(i, j int) bool { return idx[i].RunID > idx[j].RunID })
	limit := defaultRunsList
	if n := atoiOr(rest, 0); n > 0 {
		limit = n
	}
	if limit > len(idx) {
		limit = len(idx)
	}
	var sb strings.Builder
	sb.WriteString("Recent runs (newest first):\n")
	for _, e := range idx[:limit] {
		roll := ""
		if e.Rollbackable && snapshot.Exists(d.inst, e.RunID) {
			roll = "  [rollbackable]"
		}
		ws := e.Workspace
		if ws == "" {
			ws = "-"
		}
		sb.WriteString(fmt.Sprintf("  %s  %-12s ws:%-12s %-10s %d tok  %d chg%s\n",
			e.RunID, truncate(e.Chain, 12), truncate(ws, 12), e.Outcome, e.TokensTotal, e.ChangedFiles, roll))
	}
	r.Text = strings.TrimRight(sb.String(), "\n")
}

// doSnapshot takes a manual restore point of the active workspace.
func (d *Dispatcher) doSnapshot(r *model.TurnResult) {
	if d.activeWorkspace == "" {
		r.Text = "no active workspace; /ws switch <name> first"
		return
	}
	wsName := filepath.Base(d.activeWorkspace)
	id, err := snapshot.RecordPoint(d.inst, runrec.KindSnapshot, d.activeWorkspace, wsName, "", d.inst.Config.Name, version.Version, "console")
	if err != nil {
		r.IsError = true
		r.Text = "[error] snapshot: " + err.Error()
		return
	}
	_ = snapshot.Prune(d.inst, wsName, snapshotKeep)
	r.Text = "Snapshot saved as run " + id + " (restore with /rollback " + id + ")."
}

// doRollback restores the active workspace to a run's pre-state. No id previews the latest rollbackable
// run; "/rollback <id>" or "/rollback latest" executes (itself recorded as a reversible rollback run).
func (d *Dispatcher) doRollback(rest string, r *model.TurnResult) {
	if d.activeWorkspace == "" {
		r.Text = "no active workspace; /ws switch <name> first"
		return
	}
	wsName := filepath.Base(d.activeWorkspace)
	candidates := snapshot.Rollbackable(d.inst, wsName)
	if len(candidates) == 0 {
		r.Text = "nothing to roll back for workspace '" + wsName + "'"
		return
	}

	arg := strings.TrimSpace(rest)
	if arg == "" {
		latest := candidates[0]
		r.Text = fmt.Sprintf("Latest rollbackable run for '%s': %s (%s, %d changes).\nRun `/rollback %s` (or `/rollback latest`) to restore. `/runs` shows more.",
			wsName, latest.RunID, latest.Chain, latest.ChangedFiles, latest.RunID)
		return
	}

	targetID := arg
	if arg == "latest" {
		targetID = candidates[0].RunID
	}
	if !snapshot.Exists(d.inst, targetID) {
		r.Text = "run '" + targetID + "' has no snapshot (pruned beyond retention or not recorded for this workspace)"
		return
	}

	newID, changed, err := snapshot.RollbackTo(d.inst, d.activeWorkspace, wsName, targetID, d.inst.Config.Name, version.Version, "console", snapshotKeep)
	if err != nil {
		r.IsError = true
		r.Text = "[error] rollback: " + err.Error()
		return
	}
	r.Text = fmt.Sprintf("Rolled back workspace '%s' to run %s (%d files changed). This rollback is run %s; `/rollback %s` to undo it.",
		wsName, targetID, changed, newID, newID)
}

func atoiOr(s string, def int) int {
	s = strings.TrimSpace(s)
	if s == "" {
		return def
	}
	n := 0
	for _, c := range s {
		if c < '0' || c > '9' {
			return def
		}
		n = n*10 + int(c-'0')
	}
	return n
}
