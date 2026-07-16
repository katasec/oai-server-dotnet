using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Katasec.AnthropicServer;

public sealed class AnthropicServer(IChatClient chatClient, string modelId)
{
    // Registers /v1/messages on the given WebApplication
    public void Map(WebApplication app)
    {
        // The claude CLI probes HEAD / before its first real request — answer 200, not 404.
        app.MapMethods("/", ["HEAD", "GET"],
            (RequestDelegate)(ctx => { ctx.Response.StatusCode = 200; return Task.CompletedTask; }));
        app.MapPost("/v1/messages",
            (RequestDelegate)(ctx => HandleAsync(ctx, ctx.RequestAborted)));
    }

    private async Task HandleAsync(HttpContext ctx, CancellationToken ct)
    {
        try
        {
            var req = await JsonSerializer.DeserializeAsync(
                ctx.Request.Body,
                AnthropicJsonContext.Default.AnthropicRequest,
                ct);

            if (req is null) { ctx.Response.StatusCode = 400; return; }

            var chatMessages = BuildChatHistory(req);
            var chatOptions  = BuildChatOptions(req);
            var messageId    = $"msg_{Guid.NewGuid():N}";
            var model        = string.IsNullOrEmpty(req.Model) ? modelId : req.Model;

            if (req.Stream)
                await HandleStreamingAsync(ctx, chatMessages, chatOptions, messageId, model, ct);
            else
                await HandleNonStreamingAsync(ctx, chatMessages, chatOptions, messageId, model, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[AnthropicServer] 500: {ex}");
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode  = 500;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync($"{{\"error\":\"{ex.Message}\"}}", ct);
            }
        }
    }

    // Tools ride in via ChatOptions (filtered to the essentials allowlist — see ToolMapping).
    private static ChatOptions? BuildChatOptions(AnthropicRequest req)
    {
        var tools = ToolMapping.MapDeclaredTools(req);
        return tools.Count > 0 ? new ChatOptions { Tools = tools } : null;
    }

    private async Task HandleNonStreamingAsync(
        HttpContext ctx,
        List<ChatMessage> chatMessages,
        ChatOptions? chatOptions,
        string messageId,
        string model,
        CancellationToken ct)
    {
        ctx.Response.ContentType = "application/json";

        var chatResponse = await chatClient.GetResponseAsync(chatMessages, chatOptions, cancellationToken: ct);
        var (blocks, stopReason) = BuildResponseBlocks(chatResponse);

        var response = new AnthropicResponse
        {
            Id         = messageId,
            Model      = model,
            Content    = blocks,
            StopReason = stopReason,
            Usage      = new AnthropicUsage(),
        };

        await ctx.Response.WriteAsync(
            JsonSerializer.Serialize(response, AnthropicJsonContext.Default.AnthropicResponse), ct);
    }

    // The model's reply → Anthropic content blocks. A FunctionCallContent becomes a tool_use
    // block (stop_reason "tool_use") the CLIENT will execute; text stays a text block.
    private static (List<AnthropicContentBlock> Blocks, string StopReason) BuildResponseBlocks(ChatResponse chatResponse)
    {
        var blocks = new List<AnthropicContentBlock>();

        var contents = chatResponse.Messages.LastOrDefault()?.Contents ?? [];
        foreach (var part in contents)
        {
            switch (part)
            {
                case TextContent text when !string.IsNullOrEmpty(text.Text):
                    blocks.Add(new AnthropicContentBlock { Type = "text", Text = text.Text });
                    break;
                case FunctionCallContent call:
                    blocks.Add(new AnthropicContentBlock
                    {
                        Type  = "tool_use",
                        Text  = null,
                        Id    = call.CallId,
                        Name  = call.Name,
                        Input = SerializeArguments(call.Arguments),
                    });
                    break;
            }
        }

        // Legacy shape: some IChatClients surface only .Text — keep the single-text-block path.
        if (blocks.Count == 0)
            blocks.Add(new AnthropicContentBlock { Type = "text", Text = chatResponse.Text ?? string.Empty });

        var stopReason = blocks.Any(b => b.Type == "tool_use") ? "tool_use" : "end_turn";
        return (blocks, stopReason);
    }

    // AOT-safe argument serialization: values are JsonElement when they came off a wire
    // (ours or the provider's); anything else falls back to its string form.
    private static JsonElement SerializeArguments(IDictionary<string, object?>? arguments)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in arguments ?? new Dictionary<string, object?>())
            {
                writer.WritePropertyName(key);
                if (value is JsonElement element) element.WriteTo(writer);
                else if (value is null)           writer.WriteNullValue();
                else                              writer.WriteStringValue(value.ToString());
            }
            writer.WriteEndObject();
        }
        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.Clone();
    }

    private async Task HandleStreamingAsync(
        HttpContext ctx,
        List<ChatMessage> chatMessages,
        ChatOptions? chatOptions,
        string messageId,
        string model,
        CancellationToken ct)
    {
        ctx.Response.Headers["Content-Type"]  = "text/event-stream";
        ctx.Response.Headers["Cache-Control"] = "no-cache";

        await WriteEventAsync(ctx, "message_start",
            JsonSerializer.Serialize(new AnthropicMessageStart
            {
                Message = new AnthropicMessageStartPayload { Id = messageId, Model = model }
            }, AnthropicJsonContext.Default.AnthropicMessageStart), ct);

        await WriteEventAsync(ctx, "ping",
            JsonSerializer.Serialize(new AnthropicPing(), AnthropicJsonContext.Default.AnthropicPing), ct);

        // Use non-streaming to get clean extracted text from the pipeline.
        // Streaming from IChatClient yields raw StepEnvelope JSON tokens, not plain text.
        var chatResponse = await chatClient.GetResponseAsync(chatMessages, chatOptions, cancellationToken: ct);
        var (blocks, stopReason) = BuildResponseBlocks(chatResponse);

        var outputTokens = 0;
        for (var index = 0; index < blocks.Count; index++)
            outputTokens += await StreamBlockAsync(ctx, blocks[index], index, ct);

        await WriteEventAsync(ctx, "message_delta",
            JsonSerializer.Serialize(new AnthropicMessageDelta
            {
                Delta = new AnthropicMessageDeltaPayload { StopReason = stopReason },
                Usage = new AnthropicUsage { OutputTokens = outputTokens },
            }, AnthropicJsonContext.Default.AnthropicMessageDelta), ct);

        await WriteEventAsync(ctx, "message_stop",
            JsonSerializer.Serialize(new AnthropicMessageStop(), AnthropicJsonContext.Default.AnthropicMessageStop), ct);
    }

    // One content block as the start/delta/stop SSE triple. Text blocks delta the text;
    // tool_use blocks start with empty input and deliver the arguments as one input_json_delta.
    private static async Task<int> StreamBlockAsync(HttpContext ctx, AnthropicContentBlock block, int index, CancellationToken ct)
    {
        var isToolUse  = block.Type == "tool_use";
        var startBlock = isToolUse
            ? new AnthropicContentBlock { Type = "tool_use", Text = null, Id = block.Id, Name = block.Name, Input = EmptyObject }
            : new AnthropicContentBlock { Type = "text", Text = string.Empty };

        await WriteEventAsync(ctx, "content_block_start",
            JsonSerializer.Serialize(new AnthropicContentBlockStart
            {
                Index        = index,
                ContentBlock = startBlock,
            }, AnthropicJsonContext.Default.AnthropicContentBlockStart), ct);

        var payload = isToolUse
            ? JsonSerializer.Serialize(new AnthropicContentBlockToolDelta
              {
                  Index = index,
                  Delta = new AnthropicInputJsonDelta { PartialJson = block.Input?.GetRawText() ?? "{}" },
              }, AnthropicJsonContext.Default.AnthropicContentBlockToolDelta)
            : JsonSerializer.Serialize(new AnthropicContentBlockDelta
              {
                  Index = index,
                  Delta = new AnthropicTextDelta { Text = block.Text ?? string.Empty },
              }, AnthropicJsonContext.Default.AnthropicContentBlockDelta);

        if (isToolUse || !string.IsNullOrEmpty(block.Text))
            await WriteEventAsync(ctx, "content_block_delta", payload, ct);

        await WriteEventAsync(ctx, "content_block_stop",
            JsonSerializer.Serialize(new AnthropicContentBlockStop { Index = index },
                AnthropicJsonContext.Default.AnthropicContentBlockStop), ct);

        return block.Text?.Length ?? block.Input?.GetRawText().Length ?? 0;
    }

    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement;

    private static async Task WriteEventAsync(HttpContext ctx, string eventName, string data, CancellationToken ct)
    {
        await ctx.Response.WriteAsync($"event: {eventName}\ndata: {data}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }

    // Maps the wire request to provider-neutral chat messages, preserving block structure —
    // one AIContent per content block. Downstream consumers (e.g. forge's MissionChatClient)
    // rely on block boundaries surviving; do not flatten to a single string.
    public static List<ChatMessage> BuildChatHistory(AnthropicRequest req)
    {
        var messages = new List<ChatMessage>();

        if (req.System is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) } system)
            messages.Add(new ChatMessage(ChatRole.System, BuildContents(system)));

        foreach (var m in req.Messages)
        {
            var role = m.Role == "user" ? ChatRole.User : ChatRole.Assistant;
            messages.Add(new ChatMessage(role, BuildContents(m.Content)));
        }

        return messages;
    }

    // Content is a plain string or an array of typed blocks. Unknown block types are skipped
    // (parse permissively — real clients send blocks our docs never mention).
    private static List<AIContent> BuildContents(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
            return [new TextContent(content.GetString() ?? string.Empty)];

        var parts = new List<AIContent>();
        if (content.ValueKind != JsonValueKind.Array) return parts;

        foreach (var block in content.EnumerateArray())
        {
            if (!block.TryGetProperty("type", out var type)) continue;
            switch (type.GetString())
            {
                case "text" when block.TryGetProperty("text", out var text):
                    parts.Add(new TextContent(text.GetString() ?? string.Empty));
                    break;
                case "tool_use":
                    parts.Add(BuildToolUse(block));
                    break;
                case "tool_result":
                    parts.Add(BuildToolResult(block));
                    break;
            }
        }
        return parts;
    }

    private static FunctionCallContent BuildToolUse(JsonElement block)
    {
        var id   = block.TryGetProperty("id", out var idEl)     ? idEl.GetString()   ?? string.Empty : string.Empty;
        var name = block.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;

        var args = new Dictionary<string, object?>();
        if (block.TryGetProperty("input", out var input) && input.ValueKind == JsonValueKind.Object)
            foreach (var prop in input.EnumerateObject())
                args[prop.Name] = prop.Value; // JsonElement — the consumer deserializes

        return new FunctionCallContent(id, name, args);
    }

    private static FunctionResultContent BuildToolResult(JsonElement block)
    {
        var id = block.TryGetProperty("tool_use_id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
        object? result = block.TryGetProperty("content", out var content) ? ExtractText(content) : null;
        return new FunctionResultContent(id, result);
    }

    // Content can be a plain string or an array of typed content blocks
    private static string ExtractText(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
            return content.GetString() ?? string.Empty;

        if (content.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var type) && type.GetString() == "text" &&
                    block.TryGetProperty("text", out var text))
                    sb.Append(text.GetString());
            }
            return sb.ToString();
        }

        return string.Empty;
    }

    // Convenience: build and start a server from just an IChatClient
    public static WebApplication Build(IChatClient chatClient, string modelId, int port)
    {
        var server  = new AnthropicServer(chatClient, modelId);
        var builder = WebApplication.CreateSlimBuilder();

        builder.Services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, AnthropicJsonContext.Default));

        builder.Logging.AddFilter("Microsoft", LogLevel.None);
        builder.Logging.AddFilter("System", LogLevel.None);

        builder.WebHost.UseSetting("urls", $"http://0.0.0.0:{port}");

        var app = builder.Build();
        server.Map(app);
        return app;
    }
}
