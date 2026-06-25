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
// Outbound — /v1/responses response

public record OaiResponsesResponse(
    string Id,
    string Object,
    string Status,
    long CreatedAt,
    string Model,
    List<OaiResponseOutputMessage> Output);

public record OaiResponseOutputMessage(
    string Type,
    string Id,
    string Role,
    List<OaiResponseOutputContent> Content);

public record OaiResponseOutputContent(
    string Type,
    string Text,
    List<string> Annotations,
    List<string> Logprobs);

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
