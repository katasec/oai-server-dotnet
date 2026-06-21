using System.Text.Json;
using System.Text.Json.Serialization;

namespace Katasec.AnthropicServer;

// ---------------------------------------------------------------------------
// Inbound — /v1/messages request

public sealed class AnthropicRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<AnthropicMessage> Messages { get; set; } = [];

    // system can be a string or an array of content blocks in newer API versions
    [JsonPropertyName("system")]
    public JsonElement? System { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 1024;

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

// Content is string or array of content blocks — kept as JsonElement for AOT safety
public sealed class AnthropicMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public JsonElement Content { get; set; }
}

// ---------------------------------------------------------------------------
// Outbound — non-streaming /v1/messages response

public sealed class AnthropicResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "message";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "assistant";

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public List<AnthropicContentBlock> Content { get; set; } = [];

    [JsonPropertyName("stop_reason")]
    public string StopReason { get; set; } = "end_turn";

    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; set; }

    [JsonPropertyName("usage")]
    public AnthropicUsage Usage { get; set; } = new();
}

public sealed class AnthropicContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public sealed class AnthropicUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

// ---------------------------------------------------------------------------
// Outbound — streaming SSE events

public sealed class AnthropicMessageStart
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "message_start";

    [JsonPropertyName("message")]
    public AnthropicMessageStartPayload Message { get; set; } = new();
}

public sealed class AnthropicMessageStartPayload
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "message";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "assistant";

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public List<AnthropicContentBlock> Content { get; set; } = [];

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; set; }

    [JsonPropertyName("usage")]
    public AnthropicUsage Usage { get; set; } = new();
}

public sealed class AnthropicContentBlockStart
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "content_block_start";

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("content_block")]
    public AnthropicContentBlock ContentBlock { get; set; } = new();
}

public sealed class AnthropicPing
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "ping";
}

public sealed class AnthropicContentBlockDelta
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "content_block_delta";

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public AnthropicTextDelta Delta { get; set; } = new();
}

public sealed class AnthropicTextDelta
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text_delta";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public sealed class AnthropicContentBlockStop
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "content_block_stop";

    [JsonPropertyName("index")]
    public int Index { get; set; }
}

public sealed class AnthropicMessageDelta
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "message_delta";

    [JsonPropertyName("delta")]
    public AnthropicMessageDeltaPayload Delta { get; set; } = new();

    [JsonPropertyName("usage")]
    public AnthropicUsage Usage { get; set; } = new();
}

public sealed class AnthropicMessageDeltaPayload
{
    [JsonPropertyName("stop_reason")]
    public string StopReason { get; set; } = "end_turn";

    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; set; }
}

public sealed class AnthropicMessageStop
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "message_stop";
}
