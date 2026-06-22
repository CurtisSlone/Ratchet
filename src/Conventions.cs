// Conventions.cs - the instance contract in one place: the file/dir layout an ICM uses and the
// string identifiers (intents, tool kinds, flow node kinds) the engine dispatches on. Centralizing
// these kills typo drift and documents, in a single file, exactly what an instance directory may
// contain and what the runtime understands.

namespace Icm
{
    internal static class Conventions
    {
        // Top-level files an instance may provide.
        public const string ConfigFile = "ratchet.json";          // the launch config (references dirs)
        public const string LegacyConfigFile = "icm.config.json"; // oldest per-instance config (still opened)
        // Config filenames tried in order when opening a directory: the current name, then legacy names.
        public static readonly string[] ConfigCandidates = { ConfigFile, "icm.json", LegacyConfigFile };
        public const string ManifestFile = "manifest.json";
        public const string SystemFile = "SYSTEM.md";
        public const string NotesFile = "NOTES.md";   // persistent session memory the chat reads/appends
        public const string KnowledgeFile = "knowledge.json"; // live-read KB registry (name -> path), never cached
        public const string GlobalConfigFile = "icm.global.json"; // host-level base config (beside the exe, or $ICM_GLOBAL)

        // Sub-directories (relative to the instance root).
        public const string SchemasDir = "schemas";
        public const string SamplesDir = "samples";
        public const string FlowsDir = "flows";
        public const string KbDir = "kb";
        public const string RecipesDir = "recipes";          // recipe bucket (prompt + bound flow/tool); also a knowledge bucket
        public const string ToolsDir = "tools";
        public const string WorkspacesDir = "workspaces";    // container of project workspaces (replaces out/)
        public const string IndexDir = ".index";             // per-instance search-index cache (keyed by KB name)
        public const string RunsDir = "runs";                // per-chain-run state (runs/<id>/step-NNN.json)

        // Relative-path builders for the table/flow conventions.
        public static string SchemaRel(string table) { return SchemasDir + "/" + table + ".json"; }
        public static string SampleRel(string table) { return SamplesDir + "/" + table + ".txt"; }
        public static string FlowRel(string name) { return FlowsDir + "/" + name + ".json"; }

        // A routable reference file leads with a metadata block in an HTML comment (invisible in
        // rendered markdown, parseable): <!--icm { "id","title","doc_type","summary","keywords",
        // "source" } -->. `icm reindex` reads these to (re)generate manifest.json mechanically.
        public const string MetaOpen = "<!--icm";
        public const string MetaClose = "-->";

        // Folders scanned for routable reference files (markdown with an icm metadata block).
        public static readonly string[] RoutableDirs = { "reference", "patterns", "recipes", "scaffold", "snippets", "kb" };

        // Dispatcher intents (the constrained classify enum).
        internal static class Intent
        {
            public const string Ask = "ask";
            public const string Make = "make";
            public const string Validate = "validate";
            public const string Propose = "propose";
            public const string Help = "help";
            public const string Quit = "quit";
            public static readonly string[] All = { Ask, Make, Validate, Propose, Help, Quit };
        }

        // Tool kinds the host knows how to dispatch (a command/script tool uses any other kind that
        // declares a `command`/`script`).
        internal static class ToolKind
        {
            public const string Validate = "validate";
            public const string KbAnswer = "kb_answer";
            public const string Propose = "propose";
            public const string GenerateVerify = "generate_verify";
            public const string Command = "command";
            public const string Script = "script";
        }

        // Action-chain node kinds (the flow model: flows/<chain>/actions/<a>/action.json).
        internal static class ActionKind
        {
            public const string Action = "action";       // deterministic side-effect (a tools/ script) + validators
            public const string Generate = "generate";   // ICM: free-text generation via the generate seat
            public const string AiBranch = "ai_branch";  // slots -> prompt -> enum decision -> transitions
            public const string Summarizer = "summarizer"; // deterministic transform of prior outputs
            public const string Exit = "exit";           // terminal outcome
            public static readonly string[] All = { Action, Generate, AiBranch, Summarizer, Exit };
        }
    }
}
