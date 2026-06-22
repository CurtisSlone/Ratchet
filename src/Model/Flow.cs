// FlowInfo - lightweight workflow metadata for the conversational router's catalog. The node-array
// Flow model and its FlowEngine were retired in favor of action chains (Model/Chain.cs +
// Runtime/ChainEngine.cs); this metadata type remains because the router lists chains as FlowInfo.

namespace Icm
{
    // Lightweight workflow metadata for the conversational router's catalog (no chain loaded).
    internal class FlowInfo
    {
        public string Id = "";          // the chain dir name - how `/flow <id>` / `icm flow <id>` refers to it
        public string Name = "";
        public string WhenToUse = "";   // the router's match surface (a chain's summary)
    }
}
