// PathUtil - small path helpers shared by the config/registry loaders. The recurring need is
// "resolve a path that a config file named, which may be absolute or relative to that file's
// folder" - so a knowledge base or a base dir can live anywhere on disk.

using System.IO;

namespace Icm
{
    internal static class PathUtil
    {
        // Resolve `p` to an absolute path. If `p` is already rooted, return its full path; otherwise
        // resolve it relative to the FOLDER of `declaringFile` (the config that named it). Returns
        // null for an empty `p`.
        public static string ResolveAgainst(string declaringFile, string p)
        {
            if (string.IsNullOrEmpty(p)) return null;
            if (Path.IsPathRooted(p)) return Path.GetFullPath(p);
            string baseDir = ".";
            if (!string.IsNullOrEmpty(declaringFile))
            {
                string d = Path.GetDirectoryName(Path.GetFullPath(declaringFile));
                if (!string.IsNullOrEmpty(d)) baseDir = d;
            }
            return Path.GetFullPath(Path.Combine(baseDir, p));
        }
    }
}
