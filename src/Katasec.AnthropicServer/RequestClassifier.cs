using System.Text.Json;

namespace Katasec.AnthropicServer;

// What a request IS, decided before any mission logic runs (Phase 42.3 §0).
public enum RequestKind
{
    // The user's task — the only kind that runs the mission.
    Mission,
    // Client housekeeping demanding schema-shaped output (e.g. the claude CLI's title-gen).
    AuxStructuredOutput,
    // Client housekeeping in plain text (e.g. the CLI's agent-state check).
    AuxHousekeeping,
}

// Classification uses STRUCTURAL metadata only — never prompt-text sniffing. The rules are
// observed regularities from live wire captures (claude CLI 2.1.195, 2026-07-16), versioned by
// the checked-in fixtures; re-capture and re-verify on CLI version bumps.
//
//   - A real user turn declares tools (28+ observed) and adaptive thinking.
//   - Housekeeping declares NO tools and explicitly DISABLED thinking.
//   - Plain API clients (curl/python) omit `thinking` entirely, so they classify as Mission —
//     the `thinking` clause is what protects them.
public static class RequestClassifier
{
    public static RequestKind Classify(AnthropicRequest request)
    {
        if (request.Tools.Count > 0) return RequestKind.Mission;

        if (HasOutputFormat(request))    return RequestKind.AuxStructuredOutput;
        if (ThinkingDisabled(request))   return RequestKind.AuxHousekeeping;

        return RequestKind.Mission;
    }

    private static bool HasOutputFormat(AnthropicRequest request)
        => request.OutputConfig is { ValueKind: JsonValueKind.Object } config
           && config.TryGetProperty("format", out _);

    private static bool ThinkingDisabled(AnthropicRequest request)
        => request.Thinking is { ValueKind: JsonValueKind.Object } thinking
           && thinking.TryGetProperty("type", out var type)
           && type.GetString() == "disabled";
}
