// Indexer - the routable foundation (the knowledge-oracle pattern from CMMC-Claude-Companion's
// _index_generator.py, adapted to this host). Each routable reference file leads with a metadata
// block in an HTML comment:
//
//   <!--icm
//   { "id": "...", "title": "...", "doc_type": "reference",
//     "summary": "one sharp line - the only thing routing sees",
//     "keywords": ["..."], "source": { "origin": "...", "url": "...", "retrieved": "..." } }
//   -->
//
// `icm reindex` scans the routable folders, reads those blocks, and regenerates manifest.json
// MECHANICALLY (no LLM summarization) so the routing metadata lives with each file. Grounding reads
// strip the block (StripMeta) so the model sees clean content; provenance stays available for
// citations.

using System;
using System.Collections.Generic;
using System.IO;

namespace Icm
{
    internal static class Indexer
    {
        // The metadata object inside a file's <!--icm ... --> block, or null if absent/invalid.
        public static Dictionary<string, object> ExtractMeta(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int a = text.IndexOf(Conventions.MetaOpen, StringComparison.Ordinal);
            if (a < 0) return null;
            int start = a + Conventions.MetaOpen.Length;
            int b = text.IndexOf(Conventions.MetaClose, start, StringComparison.Ordinal);
            if (b < 0) return null;
            string json = text.Substring(start, b - start).Trim();
            try { return Json.AsObject(Json.Parse(json)); }
            catch { return null; }
        }

        // The file content with a leading metadata block removed (for grounding).
        public static string StripMeta(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            int a = text.IndexOf(Conventions.MetaOpen, StringComparison.Ordinal);
            if (a < 0) return text;
            int b = text.IndexOf(Conventions.MetaClose, a, StringComparison.Ordinal);
            if (b < 0) return text;
            return text.Remove(a, b + Conventions.MetaClose.Length - a).TrimStart('\r', '\n', ' ', '\t');
        }

        private static string FirstHeading(string text)
        {
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("# ")) return line.Substring(2).Trim();
            }
            return null;
        }

        // Scan the routable folders, read each file's metadata block, and regenerate manifest.json.
        public static int Reindex(Instance icm, Action<string> status)
        {
            if (status == null) status = delegate(string s) { };

            // Preserve the manifest header (name/description/domain) if present, else from config.
            string name = icm.Config.Name, description = "", domain = icm.Config.Domain;
            string manifestPath = Path.Combine(icm.Root, Conventions.ManifestFile);
            if (File.Exists(manifestPath))
            {
                try
                {
                    Dictionary<string, object> old = Json.AsObject(Json.Parse(File.ReadAllText(manifestPath)));
                    if (old != null) { name = Json.GetStringOr(old, "name", name); description = Json.GetStringOr(old, "description", description); domain = Json.GetStringOr(old, "domain", domain); }
                }
                catch { }
            }

            var entries = new List<object>();
            int count = 0;
            foreach (string d in Conventions.RoutableDirs)
            {
                string dir = Path.Combine(icm.Root, d);
                if (!Directory.Exists(dir)) continue;
                // Recursive: a layer may be organized into sub-folders (e.g. patterns/creational,
                // reference/dotnet). The sub-folder becomes the entry's `group`.
                string[] files = Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string f in files)
                {
                    string fn = Path.GetFileName(f);
                    if (fn.Equals("README.md", StringComparison.OrdinalIgnoreCase)) continue; // folder guides

                    // rel: path under the instance root, forward-slashed (e.g. "patterns/creational/builder.md").
                    string rel = f.Substring(icm.Root.Length).TrimStart('\\', '/').Replace('\\', '/');
                    // group: the folders between the layer and the file (e.g. "creational"); "" if directly in the layer.
                    string under = rel.Length > d.Length ? rel.Substring(d.Length + 1) : fn; // path below the layer dir
                    int lastSlash = under.LastIndexOf('/');
                    string group = lastSlash >= 0 ? under.Substring(0, lastSlash) : "";

                    string text;
                    try { text = File.ReadAllText(f); } catch { continue; }
                    Dictionary<string, object> meta = ExtractMeta(text);
                    if (meta == null) { status("skip (no <!--icm--> block): " + rel); continue; }

                    // Default id from the path (unique across sub-folders) when the block omits one.
                    string defaultId = (d + "/" + under).Replace(".md", "").Replace('/', '-');
                    string id = Json.GetStringOr(meta, "id", defaultId);
                    string title = Json.GetStringOr(meta, "title", FirstHeading(text) ?? id);
                    string summary = Json.GetStringOr(meta, "summary", "");
                    string docType = Json.GetStringOr(meta, "doc_type", d);
                    string grp = Json.GetStringOr(meta, "group", group);
                    var kws = new List<object>();
                    foreach (object kw in Json.GetArr(meta, "keywords")) if (kw != null) kws.Add(kw.ToString());

                    entries.Add(Json.Obj("id", id, "title", title, "path", rel,
                        "summary", summary, "doc_type", docType, "group", grp, "keywords", kws.ToArray()));
                    count++;
                }
            }

            var root = Json.Obj(
                "$comment", "Generated by `ratchet reindex` from each file's <!--icm--> metadata block. Edit the block in the source file, not this file.",
                "name", name, "description", description, "domain", domain, "entries", entries.ToArray());
            File.WriteAllText(manifestPath, Json.SerializePretty(root));
            status("reindex: wrote " + count + " entries -> " + Conventions.ManifestFile);
            return count;
        }

        // --- content-based per-library manifest (the new model: a routing index per KB dir, built
        // deterministically from file CONTENT - no <!--icm--> blocks) ---

        private static readonly string[] KbTextExt = { ".md", ".markdown", ".txt", ".text" };
        private static readonly string[] KbSkipDirs = { ".index", ".git", "bin", "obj", "dist", "node_modules" };
        private static readonly HashSet<string> Stop = new HashSet<string>(new string[] {
            "the","and","for","that","this","with","from","are","was","have","has","not","but","you","your",
            "can","will","its","use","used","using","one","two","each","when","then","into","than","also","may",
            "must","they","them","their","there","which","what","who","how","why","all","any","some","more" });

        // Build manifest entries for a KB directory from content: id from relpath, title from the first
        // heading, summary from the first prose line, keywords from the top terms. Deterministic.
        public static List<Entry> BuildManifestEntries(string dirAbs)
        {
            var entries = new List<Entry>();
            if (string.IsNullOrEmpty(dirAbs) || !Directory.Exists(dirAbs)) return entries;
            string root = Path.GetFullPath(dirAbs);
            BuildWalk(root, root, entries);
            return entries;
        }

        private static void BuildWalk(string root, string dir, List<Entry> entries)
        {
            string[] files; try { files = Directory.GetFiles(dir); } catch { files = new string[0]; }
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string f in files)
            {
                if (Array.IndexOf(KbTextExt, Path.GetExtension(f).ToLowerInvariant()) < 0) continue;
                if (Path.GetFileName(f).Equals(Conventions.ManifestFile, StringComparison.OrdinalIgnoreCase)) continue;
                if (Path.GetFileName(f).Equals("README.md", StringComparison.OrdinalIgnoreCase)) continue; // folder guides, not routable content
                string text; try { text = File.ReadAllText(f); } catch { continue; }
                text = StripMeta(text);
                string rel = f.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                var e = new Entry();
                e.Path = rel;
                string ext = Path.GetExtension(rel);
                e.Id = rel.Substring(0, rel.Length - ext.Length).Replace('/', '-');
                e.Title = FirstHeading(text) ?? Path.GetFileNameWithoutExtension(f);
                e.Summary = FirstProse(text);
                foreach (string kw in TopTerms(text, 8)) e.Keywords.Add(kw);
                entries.Add(e);
            }
            string[] subs; try { subs = Directory.GetDirectories(dir); } catch { subs = new string[0]; }
            Array.Sort(subs, StringComparer.OrdinalIgnoreCase);
            foreach (string s in subs)
                if (Array.IndexOf(KbSkipDirs, Path.GetFileName(s).ToLowerInvariant()) < 0) BuildWalk(root, s, entries);
        }

        private static string FirstProse(string text)
        {
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("```") || line.StartsWith("<") || line.StartsWith("|")) continue;
                return line.Length > 200 ? line.Substring(0, 200) : line;
            }
            return "";
        }

        private static List<string> TopTerms(string text, int n)
        {
            var counts = new Dictionary<string, int>();
            foreach (string w in Search.Tokens(text))
            {
                if (w.Length < 4 || Stop.Contains(w)) continue;
                int c; counts[w] = counts.TryGetValue(w, out c) ? c + 1 : 1;
            }
            var list = new List<KeyValuePair<string, int>>(counts);
            list.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b) { return b.Value.CompareTo(a.Value); });
            var outl = new List<string>();
            for (int i = 0; i < list.Count && outl.Count < n; i++) outl.Add(list[i].Key);
            return outl;
        }

        // Write <dirAbs>/manifest.json from content (the committed routing index for a KB library).
        public static int WriteKbManifest(string dirAbs, Action<string> status)
        {
            if (status == null) status = delegate(string s) { };
            List<Entry> entries = BuildManifestEntries(dirAbs);
            var arr = new List<object>();
            foreach (Entry e in entries)
                arr.Add(Json.Obj("id", e.Id, "title", e.Title, "path", e.Path, "summary", e.Summary, "keywords", e.Keywords.ToArray()));
            string name = Path.GetFileName(Path.GetFullPath(dirAbs).TrimEnd('\\', '/'));
            var root = Json.Obj("$comment", "Generated by `ratchet index` from file content - the routing index for this knowledge library.",
                "name", name, "entries", arr.ToArray());
            string outPath = Path.Combine(dirAbs, Conventions.ManifestFile);
            File.WriteAllText(outPath, Json.SerializePretty(root));
            status("index: wrote " + entries.Count + " entries -> " + outPath);
            return entries.Count;
        }

        // Load <dirAbs>/manifest.json into a path -> Entry map (empty if absent/unreadable).
        public static Dictionary<string, Entry> LoadManifestMap(string dirAbs)
        {
            var map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            string p = Path.Combine(dirAbs, Conventions.ManifestFile);
            if (!File.Exists(p)) return map;
            try { Manifest m = Manifest.Load(p); foreach (Entry e in m.Entries) map[e.Path] = e; }
            catch { }
            return map;
        }
    }
}
