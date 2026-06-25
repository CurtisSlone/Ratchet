// A loaded ICM: the root directory plus its config and (optional) manifest, with sandboxed file IO.
// "Open a directory and land in the ICM" is exactly Instance.Open.
//
// The file IO is deliberately scoped to the ICM root: read/write that cannot escape the instance
// directory. The host owns this so no instance tool (or model proposal) can wander the filesystem.

using System;
using System.Collections.Generic;
using System.IO;

namespace Icm
{
    // The Result<_, String> analogue: errors are carried as a message, surfaced at the CLI edge.
    internal class IcmError : Exception
    {
        public IcmError(string message) : base(message) { }
    }

    internal class Instance
    {
        public string Root;            // the workdir: the write/sandbox root (absolute, normalized)
        public Config Config;
        public Manifest Manifest;      // null when the workdir has no manifest.json

        // Open from a config FILE (ratchet.json) or a DIRECTORY (find ratchet.json, then legacy names).
        // The write/sandbox root is the config's workdir (default: the config file's folder / the dir).
        public static Instance Open(string path)
        {
            string full;
            try { full = Path.GetFullPath(path); }
            catch (Exception e) { throw new IcmError("opening " + path + ": " + e.Message); }

            var inst = new Instance();
            string cfgPath = null;
            string baseDir = full;
            if (File.Exists(full)) { cfgPath = full; baseDir = Path.GetDirectoryName(full); }
            else if (Directory.Exists(full))
            {
                foreach (string cand in Conventions.ConfigCandidates)
                {
                    string p = Path.Combine(full, cand);
                    if (File.Exists(p)) { cfgPath = p; break; }
                }
            }
            else throw new IcmError("opening " + path + ": not a file or directory");

            if (cfgPath != null) inst.Config = Config.Load(cfgPath);
            else
            {
                inst.Config = Config.Default(Path.GetFileName(full.TrimEnd('\\', '/')));
                inst.Config.SourcePath = Path.Combine(full, Conventions.ConfigFile); // resolve relative dirs against the dir
            }

            inst.Root = !string.IsNullOrEmpty(inst.Config.Workdir)
                ? PathUtil.ResolveAgainst(inst.Config.SourcePath, inst.Config.Workdir)
                : (string.IsNullOrEmpty(baseDir) ? full : baseDir);

            string manifestPath = Path.Combine(inst.Root, Conventions.ManifestFile);
            inst.Manifest = File.Exists(manifestPath) ? Manifest.Load(manifestPath) : null;
            return inst;
        }

        // --- composed read directories: a config override, else the conventional <workdir>/<name> ---
        private string DirOr(string configDir, string conv)
        {
            if (!string.IsNullOrEmpty(configDir)) return PathUtil.ResolveAgainst(Config.SourcePath, configDir);
            return Path.Combine(Root, conv);
        }
        public string FlowsDirAbs() { return DirOr(Config.FlowsDir, Conventions.FlowsDir); }
        public string ToolsDirAbs() { return DirOr(Config.ToolsDir, Conventions.ToolsDir); }
        public string SchemasDirAbs() { return DirOr(Config.SchemasDir, Conventions.SchemasDir); }
        public string SamplesDirAbs() { return DirOr(Config.SamplesDir, Conventions.SamplesDir); }
        // The projects container - a WRITE location that may point anywhere (default workdir/workspaces).
        public string WorkspacesDirAbs() { return DirOr(Config.WorkspacesDir, Conventions.WorkspacesDir); }

        // The knowledge registry composed from this config's knowledgeBases (paths resolve against the
        // config file). A conventional kb/ under the workdir is added as a default if none is declared.
        public KnowledgeRegistry Knowledge()
        {
            var reg = new KnowledgeRegistry();
            foreach (KnowledgeBase kb in Config.KnowledgeBases)
                reg.Add(kb.Name, PathUtil.ResolveAgainst(Config.SourcePath, kb.Path), kb.Default);
            if (reg.Find("kb") == null)
            {
                string kbDir = DirOr(null, Conventions.KbDir);
                if (Directory.Exists(kbDir)) reg.Add("kb", kbDir, reg.Defaults().Count == 0);
            }
            return reg;
        }

        // The composed tool set: legacy config.tools[], then the toolsDir/manifest.json declarations
        // (the offloaded config.tools[] - description/inputSchema/command/timeout), then any bare
        // toolsDir/*.ps1 script callable by name (zero-arg convention). Later sources win by name.
        public List<Tool> Tools()
        {
            var byName = new Dictionary<string, Tool>(StringComparer.OrdinalIgnoreCase);
            foreach (Tool t in Config.Tools) if (t.Name.Length > 0) byName[t.Name] = t;

            string tdir = ToolsDirAbs();
            string man = Path.Combine(tdir, Conventions.ManifestFile);
            if (File.Exists(man))
            {
                try
                {
                    Dictionary<string, object> root = Json.AsObject(Json.Parse(File.ReadAllText(man)));
                    if (root != null)
                        foreach (object o in Json.GetArr(root, "tools"))
                        {
                            var to = o as Dictionary<string, object>;
                            if (to != null) { Tool t = Tool.From(to); if (t.Name.Length > 0) byName[t.Name] = t; }
                        }
                }
                catch { }
            }
            if (Directory.Exists(tdir))
            {
                string[] scripts;
                try { scripts = Directory.GetFiles(tdir, "*.ps1"); } catch { scripts = new string[0]; }
                foreach (string f in scripts)
                {
                    string name = Path.GetFileNameWithoutExtension(f);
                    if (!byName.ContainsKey(name)) { var t = new Tool(); t.Name = name; t.Extra["script"] = f; byName[name] = t; }
                }
            }
            return new List<Tool>(byName.Values);
        }

        public Tool FindTool(string name)
        {
            foreach (Tool t in Tools())
                if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        // Resolve a relative path inside the ICM, refusing anything that escapes the root. Rejects
        // absolute paths and `..` up front, then confirms the joined path is still under root. Works
        // for not-yet-existing files (write), so it does not depend on the target existing.
        public string Resolve(string rel)
        {
            if (string.IsNullOrEmpty(rel))
                throw new IcmError("empty path");
            if (Path.IsPathRooted(rel))
                throw new IcmError("path '" + rel + "' must be relative to the ICM dir");
            foreach (string part in rel.Split('/', '\\'))
                if (part == "..")
                    throw new IcmError("path '" + rel + "' may not contain '..'");

            string joined = Path.GetFullPath(Path.Combine(Root, rel));
            string rootWithSep = Root.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string joinedWithSep = joined.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            if (!joinedWithSep.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
                throw new IcmError("path '" + rel + "' escapes the ICM dir");
            return joined;
        }

        // Path helpers - resolved against the COMPOSED read dirs (so a relocated schemas/samples/flows
        // dir works), not the write sandbox.
        public string SchemaPath(string table) { return Path.Combine(SchemasDirAbs(), table + ".json"); }
        public string SamplePath(string table) { return Path.Combine(SamplesDirAbs(), table + ".txt"); }
        public string FlowPath(string name) { return Path.Combine(FlowsDirAbs(), name + ".json"); }

        // Read an absolute path (a composed read dir may sit outside the write sandbox).
        public string ReadAt(string absPath)
        {
            try { return File.ReadAllText(absPath); }
            catch (Exception e) { throw new IcmError("reading " + absPath + ": " + e.Message); }
        }

        public string ReadFile(string rel)
        {
            string p = Resolve(rel);
            try { return File.ReadAllText(p); }
            catch (Exception e) { throw new IcmError("reading " + p + ": " + e.Message); }
        }

        public void WriteFile(string rel, string contents)
        {
            string p = Resolve(rel);
            try
            {
                string parent = Path.GetDirectoryName(p);
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                File.WriteAllText(p, contents);
            }
            catch (IcmError) { throw; }
            catch (Exception e) { throw new IcmError("writing " + p + ": " + e.Message); }
        }

        // Read a KB entry by manifest id (model-facing grounding text). The routing metadata block
        // is stripped so the model sees clean content.
        public string ReadEntry(string id)
        {
            if (Manifest == null) throw new IcmError("this ICM has no manifest.json");
            Entry e = Manifest.GetEntry(id);
            if (e == null) throw new IcmError("no manifest entry '" + id + "'");
            return Indexer.StripMeta(ReadFile(e.Path));
        }
    }
}
