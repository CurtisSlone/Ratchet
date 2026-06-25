// Knowledge - a knowledge base is an indexed directory reachable by search. The registry maps a
// name to a path (which may point ANYWHERE, inside or outside the instance). Two sources merge: the
// global base registry and the instance's knowledge.json, with the instance winning on a name
// collision. The registry is read FRESH on use (never cached) so pasting a path into knowledge.json
// takes effect on the next search.

using System;
using System.Collections.Generic;
using System.IO;

namespace Icm
{
    internal class KnowledgeBase
    {
        public string Name = "";
        public string Path = "";     // absolute once resolved through the registry
        public bool Default = false; // bare /search hits the default KB(s)

        public static List<KnowledgeBase> LoadList(List<object> arr)
        {
            var list = new List<KnowledgeBase>();
            foreach (object o in arr)
            {
                var ob = o as Dictionary<string, object>;
                if (ob == null) continue;
                var kb = new KnowledgeBase();
                kb.Name = Json.GetStringOr(ob, "name", "");
                kb.Path = Json.GetStringOr(ob, "path", "");
                kb.Default = Json.GetBool(ob, "default", false);
                if (kb.Name.Length > 0 && kb.Path.Length > 0) list.Add(kb);
            }
            return list;
        }
    }

    internal class KnowledgeRegistry
    {
        public List<KnowledgeBase> Bases = new List<KnowledgeBase>();

        // Register (or override by name) a resolved KB. The instance composes this from its config's
        // knowledgeBases - there is no base/instance merge anymore (one config).
        public void Add(string name, string absPath, bool isDefault)
        {
            for (int i = 0; i < Bases.Count; i++)
            {
                if (string.Equals(Bases[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    Bases[i].Path = absPath; Bases[i].Default = isDefault; return;
                }
            }
            var kb = new KnowledgeBase();
            kb.Name = name; kb.Path = absPath; kb.Default = isDefault;
            Bases.Add(kb);
        }

        public KnowledgeBase Find(string name)
        {
            foreach (KnowledgeBase kb in Bases)
                if (string.Equals(kb.Name, name, StringComparison.OrdinalIgnoreCase)) return kb;
            return null;
        }

        public List<KnowledgeBase> Defaults()
        {
            var o = new List<KnowledgeBase>();
            foreach (KnowledgeBase kb in Bases) if (kb.Default) o.Add(kb);
            return o;
        }
    }
}
