// KbIndex - search over a knowledge-base DIRECTORY (the kb/ or recipes/ bucket, a registered KB, or
// an ad-hoc path), as opposed to a prebuilt refdocs corpus. It walks the directory into chunks (one
// per text file), BM25-ranks them with the shared core in Search, and returns grounding text or a
// hits list. A registered KB caches its built corpus under the instance's .index/ (keyed by name,
// invalidated by a cheap file-count + mtime fingerprint), so a large KB is not re-read every query.
// An ad-hoc path is built in memory and not cached.
//
// Note: tokenization still happens per query (no persistent inverted index yet); that is a later
// perf pass. The corpus cache removes the file-read cost, which is the larger one for big KBs.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Icm
{
    internal static class KbIndex
    {
        private static readonly string[] TextExt = { ".md", ".markdown", ".txt", ".text" };
        private static readonly string[] SkipDirs = { ".index", ".git", "bin", "obj", "dist", "node_modules" };

        // Build the corpus for a directory: one chunk per text file (id = forward-slashed relative
        // path). Skips build/cache/vcs folders.
        public static List<SearchDoc> BuildCorpus(string dirAbs)
        {
            var docs = new List<SearchDoc>();
            if (string.IsNullOrEmpty(dirAbs) || !Directory.Exists(dirAbs)) return docs;
            string root = Path.GetFullPath(dirAbs);
            Walk(root, root, docs);
            return docs;
        }

        private static void Walk(string root, string dir, List<SearchDoc> docs)
        {
            string[] files;
            try { files = Directory.GetFiles(dir); } catch { files = new string[0]; }
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string f in files)
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (Array.IndexOf(TextExt, ext) < 0) continue;
                string text;
                try { text = File.ReadAllText(f); } catch { continue; }
                text = Indexer.StripMeta(text);   // drop any legacy <!--icm--> block so it doesn't pollute title/search
                string rel = f.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                var d = new SearchDoc();
                d.Id = rel; d.Kind = ext.TrimStart('.'); d.Text = text;
                d.Title = Title(text, Path.GetFileNameWithoutExtension(f));
                docs.Add(d);
            }
            string[] subs;
            try { subs = Directory.GetDirectories(dir); } catch { subs = new string[0]; }
            Array.Sort(subs, StringComparer.OrdinalIgnoreCase);
            foreach (string s in subs)
            {
                if (Array.IndexOf(SkipDirs, Path.GetFileName(s).ToLowerInvariant()) >= 0) continue;
                Walk(root, s, docs);
            }
        }

        private static string Title(string text, string fallback)
        {
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("# ")) return line.Substring(2).Trim();
                if (line.StartsWith("#")) { string t = line.TrimStart('#').Trim(); if (t.Length > 0) return t; }
                if (line.Length > 0) return line.Length > 80 ? line.Substring(0, 80) : line; // first non-empty line
            }
            return fallback;
        }

        // Search a knowledge base directory. `icm` + `cacheKey` enable the .index/ corpus cache (pass
        // null for an ad-hoc path = no cache). `hitsOnly` returns just the locations (path + title).
        public static string Query(Instance icm, string cacheKey, string dirAbs, string query, int k, bool hitsOnly)
        {
            if (string.IsNullOrEmpty(dirAbs) || !Directory.Exists(dirAbs))
                return "(knowledge base not found: " + dirAbs + ")";

            List<SearchDoc> docs = Load(icm, cacheKey, dirAbs);
            if (docs.Count == 0) return "(no indexable files under " + dirAbs + ")";

            List<KeyValuePair<int, double>> scored = Search.Bm25Scored(docs, query);
            if (scored.Count == 0) return "(no matches for '" + query + "')";

            var sb = new StringBuilder();
            int shown = 0;
            foreach (KeyValuePair<int, double> kv in scored)
            {
                if (shown++ >= k) break;
                SearchDoc d = docs[kv.Key];
                if (hitsOnly) sb.Append(d.Id + "  -  " + d.Title + "\n");
                else sb.Append("## " + d.Title + "  (" + d.Id + ")\n" + d.Text + "\n\n");
            }
            return sb.ToString().TrimEnd();
        }

        // Return the top-k file paths (relative ids) ranked for the query, via the same cached corpus
        // + BM25 as Query. The narrowing step for /search dispatch (manifest summaries on these k).
        public static List<string> Rank(Instance icm, string cacheKey, string dirAbs, string query, int k)
        {
            var outl = new List<string>();
            if (string.IsNullOrEmpty(dirAbs) || !Directory.Exists(dirAbs)) return outl;
            List<SearchDoc> docs = Load(icm, cacheKey, dirAbs);
            if (docs.Count == 0) return outl;
            List<KeyValuePair<int, double>> scored = Search.Bm25Scored(docs, query);
            for (int i = 0; i < scored.Count && outl.Count < k; i++) outl.Add(docs[scored[i].Key].Id);
            return outl;
        }

        // --- corpus cache (registered KBs only) ---

        private static List<SearchDoc> Load(Instance icm, string cacheKey, string dirAbs)
        {
            if (icm == null || string.IsNullOrEmpty(cacheKey)) return BuildCorpus(dirAbs); // ad-hoc: no cache

            string fp = Fingerprint(dirAbs);
            string cachePath;
            try { cachePath = icm.Resolve(Conventions.IndexDir + "/" + Safe(cacheKey) + ".json"); }
            catch (IcmError) { return BuildCorpus(dirAbs); }

            try
            {
                if (File.Exists(cachePath))
                {
                    Dictionary<string, object> root = Json.AsObject(Json.Parse(File.ReadAllText(cachePath)));
                    if (root != null && Json.GetStringOr(root, "fp", "") == fp)
                    {
                        var docs = new List<SearchDoc>();
                        foreach (object o in Json.GetArr(root, "docs"))
                        {
                            var dd = o as Dictionary<string, object>;
                            if (dd == null) continue;
                            var d = new SearchDoc();
                            d.Id = Json.GetStringOr(dd, "id", ""); d.Title = Json.GetStringOr(dd, "title", "");
                            d.Kind = Json.GetStringOr(dd, "kind", ""); d.Text = Json.GetStringOr(dd, "text", "");
                            docs.Add(d);
                        }
                        return docs;
                    }
                }
            }
            catch { }

            List<SearchDoc> built = BuildCorpus(dirAbs);
            try
            {
                var arr = new List<object>();
                foreach (SearchDoc d in built)
                    arr.Add(Json.Obj("id", d.Id, "title", d.Title, "kind", d.Kind, "text", d.Text));
                icm.WriteFile(Conventions.IndexDir + "/" + Safe(cacheKey) + ".json",
                    Json.Serialize(Json.Obj("fp", fp, "docs", arr.ToArray())));
            }
            catch { }
            return built;
        }

        // Cheap staleness fingerprint: indexed-file count + the latest write time. Recomputed each
        // query (metadata only, no file reads); a mismatch rebuilds the corpus.
        private static string Fingerprint(string dirAbs)
        {
            long count = 0, maxTicks = 0;
            FingerprintWalk(dirAbs, ref count, ref maxTicks);
            return count + ":" + maxTicks;
        }

        private static void FingerprintWalk(string dir, ref long count, ref long maxTicks)
        {
            string[] files;
            try { files = Directory.GetFiles(dir); } catch { return; }
            foreach (string f in files)
            {
                if (Array.IndexOf(TextExt, Path.GetExtension(f).ToLowerInvariant()) < 0) continue;
                count++;
                try { long t = File.GetLastWriteTimeUtc(f).Ticks; if (t > maxTicks) maxTicks = t; } catch { }
            }
            string[] subs;
            try { subs = Directory.GetDirectories(dir); } catch { return; }
            foreach (string s in subs)
            {
                if (Array.IndexOf(SkipDirs, Path.GetFileName(s).ToLowerInvariant()) >= 0) continue;
                FingerprintWalk(s, ref count, ref maxTicks);
            }
        }

        private static string Safe(string key)
        {
            var sb = new StringBuilder(key.Length);
            foreach (char c in key) sb.Append((char.IsLetterOrDigit(c) || c == '-' || c == '_') ? c : '_');
            return sb.ToString();
        }
    }
}
