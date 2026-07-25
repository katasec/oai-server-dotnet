namespace Katasec.AITools;

// The canonical coding-agent tool names, shared between a wire relay's inbound filter
// (Katasec.AnthropicServer) and an in-process agentic loop — one place to add a tool
// (e.g. NotebookEdit) later instead of two lists drifting apart.
public static class CodingTools
{
    public static readonly IReadOnlySet<string> EssentialNames =
        new HashSet<string>(StringComparer.Ordinal) { "Read", "Edit", "Write", "Bash" };
}
