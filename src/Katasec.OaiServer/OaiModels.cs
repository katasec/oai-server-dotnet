namespace Katasec.OaiServer;

// ---------------------------------------------------------------------------
// Outbound — /v1/models response

public record OaiModelsListResponse(
    string Object,
    List<OaiModelInfo> Data);

public record OaiModelInfo(
    string Id,
    string Object,
    long Created,
    string OwnedBy);

// ---------------------------------------------------------------------------
// Inbound — /v1/chat/completions request

public record OaiRequest(
    string Model,
    List<OaiMessage> Messages,
    bool Stream = false);

public record OaiMessage(string Role, string Content);

// ---------------------------------------------------------------------------
// Outbound — non-streaming /v1/chat/completions response

public record OaiCompletion(
    string Id,
    string Object,
    long Created,
    string Model,
    List<OaiCompletionChoice> Choices,
    OaiUsage Usage);

public record OaiCompletionChoice(
    int Index,
    OaiMessage Message,
    string FinishReason);

public record OaiUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens);

// ---------------------------------------------------------------------------
// Outbound — streaming SSE chunks

public record OaiChunk(
    string Id,
    string Object,
    long Created,
    string Model,
    List<OaiChoice> Choices);

public record OaiChoice(
    int Index,
    OaiDelta Delta,
    string? FinishReason);

public record OaiDelta(
    string? Role,
    string? Content);
