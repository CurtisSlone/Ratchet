// Search - the shared BM25-lite ranking core (SearchDoc + Bm25Scored + Tokens), used by KbIndex to
// rank a knowledge DIRECTORY against a query. The legacy refdocs corpus search + embedding rerank
// (icm docsearch / the flow `search` node) was retired with the node-array flow engine; only this
// deterministic BM25 core remains.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Icm
{
    // A single searchable chunk: an id (a relative file path), a title, a kind, and the body text
    // BM25 ranks on. Shared with KbIndex.
    internal sealed class SearchDoc
    {
        public string Id = "";
        public string Title = "";
        public string Kind = "";
        public string Text = "";
    }

    internal static class Search
    {
        private static readonly Regex TokRe = new Regex("[a-z0-9_]+", RegexOptions.Compiled);
        private const double K1 = 1.5;
        private const double B = 0.75;

        // BM25-lite scoring of a doc list against a query. Returns (index, score) pairs with score > 0,
        // sorted by score descending. The shared ranking core.
        internal static List<KeyValuePair<int, double>> Bm25Scored(List<SearchDoc> docs, string query)
        {
            var scores = new List<KeyValuePair<int, double>>();
            int n = docs.Count;
            if (n == 0) return scores;

            var tf = new List<Dictionary<string, int>>(n);
            var len = new int[n];
            var df = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                List<string> toks = Tokens(docs[i].Title + " " + docs[i].Text);
                var h = new Dictionary<string, int>();
                foreach (string w in toks) { int c; h[w] = h.TryGetValue(w, out c) ? c + 1 : 1; }
                tf.Add(h); len[i] = toks.Count;
                foreach (string w in h.Keys) { int c; df[w] = df.TryGetValue(w, out c) ? c + 1 : 1; }
            }
            double avgdl = 0; for (int i = 0; i < n; i++) avgdl += len[i]; if (n > 0) avgdl /= n;
            if (avgdl == 0) avgdl = 1;

            var qset = new HashSet<string>(Tokens(query));
            for (int i = 0; i < n; i++)
            {
                double s = 0; int dl = len[i];
                foreach (string w in qset)
                {
                    int f; if (!tf[i].TryGetValue(w, out f)) continue;
                    int dfw = df[w];
                    double idf = Math.Log(1 + (n - dfw + 0.5) / (dfw + 0.5));
                    s += idf * (f * (K1 + 1)) / (f + K1 * (1 - B + B * dl / avgdl));
                }
                if (s > 0) scores.Add(new KeyValuePair<int, double>(i, s));
            }
            scores.Sort(delegate(KeyValuePair<int, double> a, KeyValuePair<int, double> c) { return c.Value.CompareTo(a.Value); });
            return scores;
        }

        internal static List<string> Tokens(string s)
        {
            var outl = new List<string>();
            if (string.IsNullOrEmpty(s)) return outl;
            foreach (Match m in TokRe.Matches(s.ToLowerInvariant())) outl.Add(m.Value);
            return outl;
        }
    }
}
