// Spec - the generic spec engine: parse a small, human-readable spec (node / field / method / edge / op)
// and validate it against a ratchet-declared VOCABULARY (kinds, edges, value types, method modifiers).
// Domain-free mechanism; the ratchet supplies the vocabulary (what's legal) and the mapping (spec->code).
// The engine is the interpretable-IR gate: parse-or-fail + reference resolution + vocabulary conformance.
//
// v0.2 grammar (line-oriented; block + keyword-led; '#' comments; one statement per line):
//   node <kind> <Name>
//     field  <name> <type>
//     method <name> <returntype> [virtual|override|abstract|static]
//       param <name> <type>            (zero or more, belong to the preceding method)
//       `pseudocode body`              (optional, belongs to the preceding method)
//     edge <edgetype> <TargetName>
//   end
//   op <Name>   uses <Name> ...   step `free text`   end
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Icm
{
    internal class SpecProblem
    {
        public int Line; public string Message;
        public SpecProblem(int line, string msg) { Line = line; Message = msg; }
        public override string ToString() { return "line " + Line + ": " + Message; }
    }

    internal class SpecField { public string Name; public string Type; public string Value = ""; public int Line; }
    internal class SpecParam { public string Name; public string Type; }
    internal class SpecMethod { public string Name; public string Ret; public List<string> Mods = new List<string>(); public List<SpecParam> Params = new List<SpecParam>(); public string Body = ""; public int Line; }
    internal class SpecEdge { public string Type; public string Target; public int Line; }
    internal class SpecNode { public string Kind; public string Name; public int Line; public List<SpecField> Fields = new List<SpecField>(); public List<SpecMethod> Methods = new List<SpecMethod>(); public List<SpecEdge> Edges = new List<SpecEdge>(); }
    internal class SpecOp { public string Name; public int Line; public List<string> Uses = new List<string>(); public List<string> Steps = new List<string>(); }
    internal class SpecDoc { public List<SpecNode> Nodes = new List<SpecNode>(); public List<SpecOp> Ops = new List<SpecOp>(); }

    internal class EdgeDef { public List<string> From = new List<string>(); public List<string> To = new List<string>(); }
    internal class Vocabulary
    {
        public string Profile = "";
        public Dictionary<string, bool> Kinds = new Dictionary<string, bool>();   // kind -> carries behavior (may have methods)?
        public Dictionary<string, EdgeDef> Edges = new Dictionary<string, EdgeDef>();
        public HashSet<string> Types = new HashSet<string>();
        public HashSet<string> MethodModifiers = new HashSet<string>();

        public static Vocabulary Load(string path)
        {
            Vocabulary v = new Vocabulary();
            Dictionary<string, object> root = Json.AsObject(Json.Parse(File.ReadAllText(path)));
            if (root == null) throw new IcmError("vocabulary is not a JSON object: " + path);
            v.Profile = Json.GetStringOr(root, "profile", "");
            Dictionary<string, object> kinds = Json.GetObject(root, "kinds");
            if (kinds != null) foreach (KeyValuePair<string, object> kv in kinds) { Dictionary<string, object> kd = Json.AsObject(kv.Value); v.Kinds[kv.Key] = kd != null && Json.GetBool(kd, "behavior", false); }
            Dictionary<string, object> edges = Json.GetObject(root, "edges");
            if (edges != null) foreach (KeyValuePair<string, object> kv in edges)
                {
                    EdgeDef ed = new EdgeDef();
                    Dictionary<string, object> eo = Json.AsObject(kv.Value);
                    if (eo != null) { foreach (object f in Json.GetArr(eo, "from")) ed.From.Add(f.ToString()); foreach (object t in Json.GetArr(eo, "to")) ed.To.Add(t.ToString()); }
                    v.Edges[kv.Key] = ed;
                }
            foreach (object t in Json.GetArr(root, "types")) v.Types.Add(t.ToString());
            foreach (object m in Json.GetArr(root, "methodModifiers")) v.MethodModifiers.Add(m.ToString());
            return v;
        }
    }

    internal static class Spec
    {
        // A ratchet may hold several vocabularies (domains) under spec/. Default is spec/vocab.json;
        // a named profile selects spec/<profile>.json (e.g. "gui" -> spec/gui.json).
        public static string VocabPath(Instance icm, string profile)
        {
            string fn = string.IsNullOrEmpty(profile) ? "vocab.json" : (profile + ".json");
            return Path.Combine(icm.Root, Path.Combine("spec", fn));
        }

        public static SpecDoc Parse(string text, List<SpecProblem> problems)
        {
            SpecDoc doc = new SpecDoc();
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            SpecNode curNode = null; SpecOp curOp = null; SpecMethod curMethod = null;
            for (int i = 0; i < lines.Length; i++)
            {
                int ln = i + 1;
                string raw = lines[i];
                int hash = raw.IndexOf('#'); if (hash >= 0) raw = raw.Substring(0, hash);
                string line = raw.Trim();
                if (line.Length == 0) continue;

                if (curNode == null && curOp == null)
                {
                    string head, rest; SplitHead(line, out head, out rest);
                    if (head == "node") { string[] a = Words(rest); if (a.Length < 2) { problems.Add(new SpecProblem(ln, "node needs: node <kind> <Name>")); continue; } curNode = new SpecNode(); curNode.Kind = a[0]; curNode.Name = a[a.Length - 1]; curNode.Line = ln; curMethod = null; }  // Name is the LAST token, so "node abstract class Account" works (kind=abstract, name=Account)
                    else if (head == "op") { string[] a = Words(rest); if (a.Length < 1) { problems.Add(new SpecProblem(ln, "op needs: op <Name>")); continue; } curOp = new SpecOp(); curOp.Name = a[0]; curOp.Line = ln; }
                    else problems.Add(new SpecProblem(ln, "expected 'node' or 'op', got '" + head + "'"));
                }
                else if (curNode != null)
                {
                    if (line[0] == '`') { if (curMethod == null) problems.Add(new SpecProblem(ln, "pseudocode body with no preceding method")); else curMethod.Body = Unquote(line); continue; }
                    string head, rest; SplitHead(line, out head, out rest);
                    if (head == "end") { doc.Nodes.Add(curNode); curNode = null; curMethod = null; }
                    else if (head == "field") { int eq = rest.IndexOf('='); string decl = eq >= 0 ? rest.Substring(0, eq).Trim() : rest; string val = eq >= 0 ? rest.Substring(eq + 1).Trim() : ""; string[] a = Words(decl); if (a.Length < 2) { problems.Add(new SpecProblem(ln, "field needs: field <name> <type> [= <value>]")); continue; } SpecField f = new SpecField(); f.Name = a[0]; f.Type = a[1]; f.Value = Unquote(val); f.Line = ln; curNode.Fields.Add(f); curMethod = null; }
                    else if (head == "method") { string[] a = Words(rest); if (a.Length < 2) { problems.Add(new SpecProblem(ln, "method needs: method <name> <returntype> [modifiers]")); continue; } SpecMethod m = new SpecMethod(); m.Name = a[0]; m.Ret = a[1]; m.Line = ln; for (int k = 2; k < a.Length; k++) m.Mods.Add(a[k]); curNode.Methods.Add(m); curMethod = m; }
                    else if (head == "param") { string[] a = Words(rest); if (curMethod == null) { problems.Add(new SpecProblem(ln, "param with no preceding method")); continue; } if (a.Length < 2) { problems.Add(new SpecProblem(ln, "param needs: param <name> <type>")); continue; } SpecParam p = new SpecParam(); p.Name = a[0]; p.Type = a[1]; curMethod.Params.Add(p); }
                    else if (head == "edge") { string[] a = Words(rest); if (a.Length < 2) { problems.Add(new SpecProblem(ln, "edge needs: edge <type> <Target>")); continue; } SpecEdge e = new SpecEdge(); e.Type = a[0]; e.Target = a[1]; e.Line = ln; curNode.Edges.Add(e); curMethod = null; }
                    else if (Words(rest).Length == 1) { SpecEdge e = new SpecEdge(); e.Type = head; e.Target = rest.Trim(); e.Line = ln; curNode.Edges.Add(e); curMethod = null; }  // bare edge: "extends Account" == "edge extends Account"
                    else problems.Add(new SpecProblem(ln, "inside node: expected field/method/param/edge/end, got '" + head + "'"));
                }
                else
                {
                    if (line[0] == '`') { curOp.Steps.Add(Unquote(line)); continue; }
                    string head, rest; SplitHead(line, out head, out rest);
                    if (head == "end") { doc.Ops.Add(curOp); curOp = null; }
                    else if (head == "uses") { foreach (string u in Words(rest)) curOp.Uses.Add(u); }
                    else if (head == "step") { curOp.Steps.Add(Unquote(rest)); }
                    else problems.Add(new SpecProblem(ln, "inside op: expected uses/step/end, got '" + head + "'"));
                }
            }
            if (curNode != null) problems.Add(new SpecProblem(curNode.Line, "node '" + curNode.Name + "' is missing 'end'"));
            if (curOp != null) problems.Add(new SpecProblem(curOp.Line, "op '" + curOp.Name + "' is missing 'end'"));
            return doc;
        }

        public static void Validate(SpecDoc doc, Vocabulary v, List<SpecProblem> problems)
        {
            HashSet<string> names = new HashSet<string>();
            foreach (SpecNode n in doc.Nodes) if (!names.Add(n.Name)) problems.Add(new SpecProblem(n.Line, "duplicate node name '" + n.Name + "'"));

            foreach (SpecNode n in doc.Nodes)
            {
                bool kindOk = v.Kinds.ContainsKey(n.Kind);
                if (!kindOk) problems.Add(new SpecProblem(n.Line, "unknown kind '" + n.Kind + "' (vocabulary: " + Join(v.Kinds.Keys) + ")"));
                if (kindOk && !v.Kinds[n.Kind] && n.Methods.Count > 0) problems.Add(new SpecProblem(n.Line, "kind '" + n.Kind + "' carries no behavior; methods not allowed"));

                foreach (SpecField f in n.Fields)
                {
                    if (!Resolves(f.Type, v, names)) problems.Add(new SpecProblem(f.Line, "unresolved type '" + f.Type + "' on field '" + f.Name + "'"));
                    else if (f.Value.Length > 0 && !ValueOk(f.Type, f.Value)) problems.Add(new SpecProblem(f.Line, "value '" + f.Value + "' is not a valid " + f.Type));
                }
                foreach (SpecMethod m in n.Methods)
                {
                    if (!Resolves(m.Ret, v, names)) problems.Add(new SpecProblem(m.Line, "unresolved return type '" + m.Ret + "' on method '" + m.Name + "'"));
                    foreach (string mod in m.Mods) if (v.MethodModifiers.Count > 0 && !v.MethodModifiers.Contains(mod)) problems.Add(new SpecProblem(m.Line, "unknown method modifier '" + mod + "' (vocabulary: " + Join(v.MethodModifiers) + ")"));
                    foreach (SpecParam p in m.Params) if (!Resolves(p.Type, v, names)) problems.Add(new SpecProblem(m.Line, "unresolved type '" + p.Type + "' on param '" + p.Name + "'"));
                }
                foreach (SpecEdge e in n.Edges)
                {
                    if (!v.Edges.ContainsKey(e.Type)) problems.Add(new SpecProblem(e.Line, "unknown edge type '" + e.Type + "' (vocabulary: " + Join(v.Edges.Keys) + ")"));
                    else if (v.Edges[e.Type].From.Count > 0 && !v.Edges[e.Type].From.Contains(n.Kind)) problems.Add(new SpecProblem(e.Line, "edge '" + e.Type + "' not allowed from kind '" + n.Kind + "'"));
                    if (!names.Contains(e.Target)) problems.Add(new SpecProblem(e.Line, "edge '" + e.Type + "' targets undefined node '" + e.Target + "'"));
                    else if (v.Edges.ContainsKey(e.Type) && v.Edges[e.Type].To.Count > 0) { string tk = KindOf(doc, e.Target); if (tk != null && !v.Edges[e.Type].To.Contains(tk)) problems.Add(new SpecProblem(e.Line, "edge '" + e.Type + "' not allowed to kind '" + tk + "'")); }
                }
            }
            foreach (SpecOp o in doc.Ops) foreach (string u in o.Uses) if (!names.Contains(u)) problems.Add(new SpecProblem(o.Line, "op '" + o.Name + "' uses undefined node '" + u + "'"));
        }

        // Emit the validated spec as a JSON AST for a ratchet's mapping tool to consume (spec -> code).
        public static string ToJson(SpecDoc d)
        {
            var nodes = new List<object>();
            foreach (SpecNode n in d.Nodes)
            {
                var fields = new List<object>(); foreach (SpecField f in n.Fields) fields.Add(Json.Obj("name", f.Name, "type", f.Type, "value", f.Value));
                var methods = new List<object>();
                foreach (SpecMethod m in n.Methods)
                {
                    var ps = new List<object>(); foreach (SpecParam p in m.Params) ps.Add(Json.Obj("name", p.Name, "type", p.Type));
                    methods.Add(Json.Obj("name", m.Name, "ret", m.Ret, "mods", m.Mods.ToArray(), "params", ps.ToArray(), "body", m.Body));
                }
                var edges = new List<object>(); foreach (SpecEdge e in n.Edges) edges.Add(Json.Obj("type", e.Type, "target", e.Target));
                nodes.Add(Json.Obj("kind", n.Kind, "name", n.Name, "fields", fields.ToArray(), "methods", methods.ToArray(), "edges", edges.ToArray()));
            }
            var ops = new List<object>();
            foreach (SpecOp o in d.Ops) ops.Add(Json.Obj("name", o.Name, "uses", o.Uses.ToArray(), "steps", o.Steps.ToArray()));
            return Json.SerializePretty(Json.Obj("nodes", nodes.ToArray(), "ops", ops.ToArray()));
        }

        private static bool Resolves(string type, Vocabulary v, HashSet<string> names) { return v.Types.Contains(type) || names.Contains(type); }
        private static bool ValueOk(string type, string val)
        {
            if (type == "int" || type == "long") { long l; return long.TryParse(val, out l); }
            if (type == "bool") { return val == "true" || val == "false"; }
            if (type == "double") { double d; return double.TryParse(val, out d); }
            return true; // string and node types accept any literal
        }
        private static string KindOf(SpecDoc doc, string name) { foreach (SpecNode n in doc.Nodes) if (n.Name == name) return n.Kind; return null; }
        private static void SplitHead(string s, out string head, out string rest) { int sp = s.IndexOf(' '); if (sp < 0) { head = s; rest = ""; } else { head = s.Substring(0, sp); rest = s.Substring(sp + 1).Trim(); } }
        private static string[] Words(string s) { if (s.Length == 0) return new string[0]; return s.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries); }
        private static string Unquote(string s) { s = s.Trim(); if (s.Length >= 2 && s[0] == '`' && s[s.Length - 1] == '`') return s.Substring(1, s.Length - 2); return s; }
        private static string Join(IEnumerable<string> xs) { StringBuilder b = new StringBuilder(); bool first = true; foreach (string x in xs) { if (!first) b.Append(", "); b.Append(x); first = false; } return b.ToString(); }
    }
}
