using System.Text.Json;
using Katasec.AITools;
using Microsoft.Extensions.AI;

namespace Katasec.AnthropicServer;

// Neutral tool mapping (Phase 42.3 task 1): Anthropic wire tool shapes ↔ Microsoft.Extensions.AI.
// The server is a RELAY — declarations in (filtered), the model's tool_use back to the client,
// the client executes locally, tool_result relayed upstream. Nothing here ever executes a tool.
public static class ToolMapping
{
    // Essentials allowlist (DECIDED 2026-07-16). Only these client tools are shown to the
    // mission's model. Everything else — MCP connectors (mcp__*), subagents, harness niceties —
    // is never forwarded: safety (no Gmail-write emission), privacy (connector inventory stays
    // client-side), cost (~28KB of schemas otherwise ride every call), capability (4 well-chosen
    // tools beat 57 alien schemas). Outbound needs no filter: the model can only call what it saw.
    // Shared with any in-process agentic loop via Katasec.AITools.CodingTools — one list, not two.
    public static readonly IReadOnlySet<string> EssentialTools = CodingTools.EssentialNames;

    // Inbound declarations → M.E.AI tools, filtered to the essentials. DeclaredTool (Katasec.AITools)
    // is a declaration-only relay: it carries the client's schema verbatim and is never invoked here.
    public static List<AITool> MapDeclaredTools(AnthropicRequest request)
        => request.Tools
            .Where(t => EssentialTools.Contains(t.Name))
            .Select(AITool (t) => new DeclaredTool(t.Name, t.Description ?? string.Empty, t.InputSchema))
            .ToList();

    // A tool continuation is a request whose last message ends in a tool_result block —
    // the client is handing back a tool's output, not a new user turn.
    public static bool IsToolContinuation(AnthropicRequest request)
    {
        var last = request.Messages.LastOrDefault();
        if (last is null || last.Content.ValueKind != JsonValueKind.Array) return false;

        JsonElement? lastBlock = null;
        foreach (var block in last.Content.EnumerateArray()) lastBlock = block;

        return lastBlock is { } b
            && b.TryGetProperty("type", out var type)
            && type.GetString() == "tool_result";
    }
}
