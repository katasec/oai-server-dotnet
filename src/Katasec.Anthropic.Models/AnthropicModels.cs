using System.Text.Json;
using System.Text.Json.Serialization;

namespace Katasec.Anthropic.Models;

// ---------------------------------------------------------------------------
// Request — /v1/messages

public sealed class AnthropicRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<AnthropicMessage> Messages { get; set; } = [];

    // system can be a string or an array of content blocks in newer API versions
    [JsonPropertyName("system")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? System { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 1024;

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    // Tool declarations the client offers the model; null means no tools. The server is a relay:
    // these are forwarded (filtered) to the model; the CLIENT executes whatever the model calls.
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AnthropicToolDefinition>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AnthropicToolChoice? ToolChoice { get; set; }

    [JsonPropertyName("stop_sequences")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? StopSequences { get; set; }

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? TopP { get; set; }

    [JsonPropertyName("top_k")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TopK { get; set; }

    // Extended-thinking config. Structural classifier signal (Phase 42.3): the claude CLI
    // always sends it — adaptive on real turns, disabled on housekeeping calls.
    [JsonPropertyName("thinking")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Thinking { get; set; }

    // Structured-output demand (e.g. the CLI's title-gen {title} schema) — classifier signal.
    [JsonPropertyName("output_config")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? OutputConfig { get; set; }
}

public sealed class AnthropicToolChoice
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "auto";

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("disable_parallel_tool_use")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DisableParallelToolUse { get; set; }
}

public sealed class AnthropicToolDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("input_schema")]
    public JsonElement InputSchema { get; set; }
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
// Response — non-streaming /v1/messages

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

// A response content block: text ({type, text}) or tool_use ({type, id, name, input}).
// On the request side it is also a tool_result ({type, tool_use_id, content, is_error}).
// Null members are omitted on the wire, so one class covers every shape AOT-safely.
public sealed class AnthropicContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("input")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Input { get; set; }

    [JsonPropertyName("tool_use_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolUseId { get; set; }

    // tool_result content: a string or an array of content blocks.
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Content { get; set; }

    [JsonPropertyName("is_error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsError { get; set; }
}

public sealed class AnthropicUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("cache_creation_input_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CacheCreationInputTokens { get; set; }

    [JsonPropertyName("cache_read_input_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CacheReadInputTokens { get; set; }
}

// ---------------------------------------------------------------------------
// Response — streaming SSE events

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

// content_block_delta carrying a tool_use block's arguments as partial JSON.
public sealed class AnthropicContentBlockToolDelta
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "content_block_delta";

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public AnthropicInputJsonDelta Delta { get; set; } = new();
}

public sealed class AnthropicInputJsonDelta
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "input_json_delta";

    [JsonPropertyName("partial_json")]
    public string PartialJson { get; set; } = string.Empty;
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

// ---------------------------------------------------------------------------
// Response — errors

// Both the non-2xx body and the SSE `event: error` data: {"type":"error","error":{"type","message"}}.
public sealed class AnthropicErrorResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "error";

    [JsonPropertyName("error")]
    public AnthropicErrorDetail Error { get; set; } = new();
}

public sealed class AnthropicErrorDetail
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
