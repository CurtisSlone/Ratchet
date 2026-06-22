// ChainLint - the author-time validator for an action chain (the FlowLint analog for the new model,
// at a validate.py-equivalent level). Catches what would otherwise be a runtime failure
// with a loose model inside an unbounded graph:
//   - unknown/missing node kinds
//   - chain.json `nodes` vs the action.json files on disk
//   - every edge target (on_success/on_failure/transitions) is a declared node
//   - ai_branch `transitions` keys == `output_schema.next.enum`
//   - every `inputs[].from` is a reachable PREDECESSOR (BFS from entry)
//   - a referenced tool is declared; a ref/search binding names a library; a binding has an `as`
//   - ai_branch prompt.md exists and fits a rough token budget
// Pure + (mostly) static so SelfTest can cover it with an in-memory Chain.

using System;
using System.Collections.Generic;
using System.IO;

namespace Icm
{
    internal static class ChainLint
    {
        private const int CharsPerToken = 4;
        private const int PromptBodyLimit = 600;   // tokens, per context-budget heuristic

        public static List<string> Check(Chain c, List<string> toolNames)
        {
            var p = new List<string>();
            if (c == null) { p.Add("chain is null"); return p; }
            if (string.IsNullOrEmpty(c.Entry)) p.Add("chain has no 'entry'");

            var onDisk = new HashSet<string>(c.Actions.Keys);
            var declared = new HashSet<string>(c.NodeIds);
            foreach (string m in declared) if (!onDisk.Contains(m)) p.Add("declared node '" + m + "' has no action.json on disk");
            foreach (string x in onDisk) if (!declared.Contains(x)) p.Add("action '" + x + "' not declared in chain.json nodes");
            if (c.Entry.Length > 0 && !onDisk.Contains(c.Entry)) p.Add("entry '" + c.Entry + "' has no action.json");

            foreach (KeyValuePair<string, ActionNode> kv in c.Actions)
            {
                ActionNode a = kv.Value;
                string w = "node '" + a.Id + "'";
                if (a.Kind.Length == 0) { p.Add(w + ": missing 'kind'"); continue; }
                if (Array.IndexOf(Conventions.ActionKind.All, a.Kind) < 0) { p.Add(w + ": unknown kind '" + a.Kind + "'"); continue; }

                foreach (string tgt in a.Edges())
                    if (!onDisk.Contains(tgt)) p.Add(w + ": edge -> '" + tgt + "' is not a declared node");

                foreach (InputBinding ib in a.Inputs)
                {
                    if (string.IsNullOrEmpty(ib.As)) p.Add(w + ": an input binding has no 'as'");
                    if (ib.Source == "ref" && string.IsNullOrEmpty(ib.Lib)) p.Add(w + ": ref binding has no library");
                    if (ib.Source == "search" && string.IsNullOrEmpty(ib.Lib)) p.Add(w + ": search binding has no library");
                    if (ib.Source.Length == 0) p.Add(w + ": input '" + ib.As + "' has no source (from/ref/search)");
                }

                if (a.Kind == Conventions.ActionKind.Action)
                {
                    if (string.IsNullOrEmpty(a.Tool) && string.IsNullOrEmpty(a.Endpoint)) p.Add(w + ": action needs a 'tool' (or 'endpoint')");
                    else if (!string.IsNullOrEmpty(a.Tool) && toolNames != null && !toolNames.Contains(a.Tool)) p.Add(w + ": references unknown tool '" + a.Tool + "'");
                    if (string.IsNullOrEmpty(a.OnSuccess)) p.Add(w + ": action needs 'on_success'");
                }
                else if (a.Kind == Conventions.ActionKind.Generate)
                {
                    if (string.IsNullOrEmpty(a.Prompt)) p.Add(w + ": generate needs 'prompt'");
                    if (string.IsNullOrEmpty(a.OnSuccess)) p.Add(w + ": generate needs 'on_success'");
                    CheckPrompt(a, w, p);
                }
                else if (a.Kind == Conventions.ActionKind.AiBranch)
                {
                    if (string.IsNullOrEmpty(a.Prompt)) p.Add(w + ": ai_branch needs 'prompt'");
                    if (a.Transitions.Count < 2) p.Add(w + ": ai_branch needs at least 2 transitions");
                    var keys = new HashSet<string>(a.Transitions.Keys);
                    HashSet<string> enumVals = NextEnum(a.OutputSchema);
                    if (!SetEq(keys, enumVals)) p.Add(w + ": transitions keys {" + Join(keys) + "} != output_schema.next.enum {" + Join(enumVals) + "}");
                    CheckPrompt(a, w, p);
                }
                else if (a.Kind == Conventions.ActionKind.Exit)
                {
                    if (string.IsNullOrEmpty(a.Outcome)) p.Add(w + ": exit needs 'outcome'");
                }
            }

            // inputs[].from must be a reachable predecessor (BFS from entry)
            Dictionary<string, int> order = Bfs(c);
            foreach (KeyValuePair<string, ActionNode> kv in c.Actions)
            {
                ActionNode a = kv.Value;
                int co; bool haveCo = order.TryGetValue(a.Id, out co);
                foreach (InputBinding ib in a.Inputs)
                {
                    if (ib.Source != "from" || string.IsNullOrEmpty(ib.From)) continue;
                    // reserved run seeds ($input/$workspace) and chain-declared inputs are always available
                    if (ib.From == "$input" || ib.From == "$workspace" || c.Inputs.Contains(ib.From)) continue;
                    int so;
                    if (!order.TryGetValue(ib.From, out so)) p.Add("node '" + a.Id + "': inputs.from '" + ib.From + "' is not reachable from entry");
                    else if (haveCo && so >= co) p.Add("node '" + a.Id + "': inputs.from '" + ib.From + "' is not a predecessor");
                }
            }
            return p;
        }

        private static void CheckPrompt(ActionNode a, string w, List<string> p)
        {
            if (string.IsNullOrEmpty(a.Prompt) || string.IsNullOrEmpty(a.Dir)) return; // Prompt missing already reported / in-memory chain
            string rel = a.Prompt.Replace("./", "").Replace(".\\", "");
            string path = Path.Combine(a.Dir, rel);
            if (!File.Exists(path)) { p.Add(w + ": prompt file '" + a.Prompt + "' not found"); return; }
            try
            {
                int tokens = (File.ReadAllText(path).Length + CharsPerToken - 1) / CharsPerToken;
                if (tokens > PromptBodyLimit) p.Add(w + ": prompt body " + tokens + " tokens > limit " + PromptBodyLimit);
            }
            catch { }
        }

        private static Dictionary<string, int> Bfs(Chain c)
        {
            var order = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(c.Entry) || !c.Actions.ContainsKey(c.Entry)) return order;
            var q = new Queue<string>();
            order[c.Entry] = 0; q.Enqueue(c.Entry);
            while (q.Count > 0)
            {
                string cur = q.Dequeue();
                ActionNode a; if (!c.Actions.TryGetValue(cur, out a)) continue;
                foreach (string n in a.Edges())
                    if (!string.IsNullOrEmpty(n) && !order.ContainsKey(n)) { order[n] = order[cur] + 1; q.Enqueue(n); }
            }
            return order;
        }

        private static HashSet<string> NextEnum(Dictionary<string, object> schema)
        {
            var s = new HashSet<string>();
            if (schema == null) return s;
            Dictionary<string, object> props = Json.GetObject(schema, "properties");
            Dictionary<string, object> next = props != null ? Json.GetObject(props, "next") : null;
            if (next != null) foreach (object e in Json.GetArr(next, "enum")) if (e != null) s.Add(e.ToString());
            return s;
        }

        private static bool SetEq(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count != b.Count) return false;
            foreach (string x in a) if (!b.Contains(x)) return false;
            return true;
        }

        private static string Join(HashSet<string> s)
        {
            var l = new List<string>(s); l.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(",", l.ToArray());
        }
    }
}
