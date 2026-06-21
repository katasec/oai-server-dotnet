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
            var messageId    = $"msg_{Guid.NewGuid():N}";
            var model        = string.IsNullOrEmpty(req.Model) ? modelId : req.Model;

            if (req.Stream)
                await HandleStreamingAsync(ctx, chatMessages, messageId, model, ct);
            else
                await HandleNonStreamingAsync(ctx, chatMessages, messageId, model, ct);
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

    private async Task HandleNonStreamingAsync(
        HttpContext ctx,
        List<ChatMessage> chatMessages,
        string messageId,
        string model,
        CancellationToken ct)
    {
        ctx.Response.ContentType = "application/json";

        var chatResponse = await chatClient.GetResponseAsync(chatMessages, cancellationToken: ct);
        var replyText    = chatResponse.Text ?? string.Empty;

        var response = new AnthropicResponse
        {
            Id      = messageId,
            Model   = model,
            Content = [new AnthropicContentBlock { Text = replyText }],
            Usage   = new AnthropicUsage(),
        };

        await ctx.Response.WriteAsync(
            JsonSerializer.Serialize(response, AnthropicJsonContext.Default.AnthropicResponse), ct);
    }

    private async Task HandleStreamingAsync(
        HttpContext ctx,
        List<ChatMessage> chatMessages,
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

        await WriteEventAsync(ctx, "content_block_start",
            JsonSerializer.Serialize(new AnthropicContentBlockStart
            {
                ContentBlock = new AnthropicContentBlock { Text = string.Empty }
            }, AnthropicJsonContext.Default.AnthropicContentBlockStart), ct);

        await WriteEventAsync(ctx, "ping",
            JsonSerializer.Serialize(new AnthropicPing(), AnthropicJsonContext.Default.AnthropicPing), ct);

        // Use non-streaming to get clean extracted text from the pipeline.
        // Streaming from IChatClient yields raw StepEnvelope JSON tokens, not plain text.
        var chatResponse = await chatClient.GetResponseAsync(chatMessages, cancellationToken: ct);
        var replyText    = chatResponse.Text ?? string.Empty;
        var outputTokens = replyText.Length;

        if (!string.IsNullOrEmpty(replyText))
            await WriteEventAsync(ctx, "content_block_delta",
                JsonSerializer.Serialize(new AnthropicContentBlockDelta
                {
                    Delta = new AnthropicTextDelta { Text = replyText }
                }, AnthropicJsonContext.Default.AnthropicContentBlockDelta), ct);

        await WriteEventAsync(ctx, "content_block_stop",
            JsonSerializer.Serialize(new AnthropicContentBlockStop(), AnthropicJsonContext.Default.AnthropicContentBlockStop), ct);

        await WriteEventAsync(ctx, "message_delta",
            JsonSerializer.Serialize(new AnthropicMessageDelta
            {
                Delta = new AnthropicMessageDeltaPayload(),
                Usage = new AnthropicUsage { OutputTokens = outputTokens },
            }, AnthropicJsonContext.Default.AnthropicMessageDelta), ct);

        await WriteEventAsync(ctx, "message_stop",
            JsonSerializer.Serialize(new AnthropicMessageStop(), AnthropicJsonContext.Default.AnthropicMessageStop), ct);
    }

    private static async Task WriteEventAsync(HttpContext ctx, string eventName, string data, CancellationToken ct)
    {
        await ctx.Response.WriteAsync($"event: {eventName}\ndata: {data}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }

    private static List<ChatMessage> BuildChatHistory(AnthropicRequest req)
    {
        var messages = new List<ChatMessage>();

        if (req.System.HasValue && req.System.Value.ValueKind != JsonValueKind.Undefined)
            messages.Add(new ChatMessage(ChatRole.System, ExtractText(req.System.Value)));

        foreach (var m in req.Messages)
        {
            var role = m.Role == "user" ? ChatRole.User : ChatRole.Assistant;
            messages.Add(new ChatMessage(role, ExtractText(m.Content)));
        }

        return messages;
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
