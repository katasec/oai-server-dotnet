using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Katasec.OaiServer;

public sealed class OaiServer(IChatClient chatClient, ISessionStore sessionStore, string agentId)
{
    private const string SessionHeader = "X-Session-Id";

    // Registers /v1/chat/completions and /v1/models on the given WebApplication
    public void Map(WebApplication app)
    {
        // Explicit RequestDelegate cast is AOT-safe — no parameter reflection needed
        app.MapPost("/v1/chat/completions",
            (RequestDelegate)(ctx => HandleAsync(ctx, ctx.RequestAborted)));

        app.MapGet("/v1/models",
            (RequestDelegate)(ctx => HandleModelsAsync(ctx, ctx.RequestAborted)));

        app.MapPost("/v1/responses",
            (RequestDelegate)(ctx => HandleResponsesAsync(ctx, ctx.RequestAborted)));
    }

    private async Task HandleModelsAsync(HttpContext ctx, CancellationToken ct)
    {
        var response = new OaiModelsListResponse(
            Object: "list",
            Data:   [new OaiModelInfo(
                Id:       agentId,
                Object:   "model",
                Created:  DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                OwnedBy:  "forge")]);

        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(
            JsonSerializer.Serialize(response, OaiJsonContext.Default.OaiModelsListResponse),
            ct);
    }

    private async Task HandleAsync(HttpContext ctx, CancellationToken ct)
    {
        // --- Parse request
        var req = await JsonSerializer.DeserializeAsync(
            ctx.Request.Body,
            OaiJsonContext.Default.OaiRequest,
            ct);

        if (req is null) { ctx.Response.StatusCode = 400; return; }

        // --- Session: load or create
        var sessionId = ctx.Request.Headers[SessionHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        var session = await sessionStore.GetAsync(sessionId, ct) ?? new Session { Id = sessionId };

        // --- The last user message maps to the mission's goal parameter
        var userMessage = req.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty;

        // Append incoming message to history
        session.History.Add(new OaiMessage("user", userMessage));

        var responseId = $"chatcmpl-{Guid.NewGuid():N}";
        var created    = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Build chat history for the IChatClient
        var chatMessages = session.History
            .Select(m => new ChatMessage(
                m.Role == "user" ? ChatRole.User : ChatRole.Assistant,
                m.Content))
            .ToList();

        ctx.Response.Headers[SessionHeader] = sessionId;

        if (req.Stream)
        {
            // --- Streaming SSE response
            ctx.Response.Headers["Content-Type"]  = "text/event-stream";
            ctx.Response.Headers["Cache-Control"] = "no-cache";

            var fullReply = new StringBuilder();
            var firstChunk = true;

            await foreach (var update in chatClient.GetStreamingResponseAsync(chatMessages, cancellationToken: ct))
            {
                var text = update.Text ?? string.Empty;
                fullReply.Append(text);

                // Spec requires role:"assistant" in the first chunk only; subsequent chunks omit it.
                var role = firstChunk ? "assistant" : null;
                firstChunk = false;

                var chunk = new OaiChunk(
                    Id:      responseId,
                    Object:  "chat.completion.chunk",
                    Created: created,
                    Model:   agentId,
                    Choices: [new OaiChoice(0, new OaiDelta(role, text), null)]);

                var json = JsonSerializer.Serialize(chunk, OaiJsonContext.Default.OaiChunk);
                await ctx.Response.WriteAsync($"data: {json}\n\n", ct);
                await ctx.Response.Body.FlushAsync(ct);
            }

            // Final chunk with finish_reason before [DONE]
            var finalChunk = new OaiChunk(
                Id:      responseId,
                Object:  "chat.completion.chunk",
                Created: created,
                Model:   agentId,
                Choices: [new OaiChoice(0, new OaiDelta(null, null), "stop")]);
            var finalJson = JsonSerializer.Serialize(finalChunk, OaiJsonContext.Default.OaiChunk);
            await ctx.Response.WriteAsync($"data: {finalJson}\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);

            await ctx.Response.WriteAsync("data: [DONE]\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);

            session.History.Add(new OaiMessage("assistant", fullReply.ToString()));
        }
        else
        {
            // --- Non-streaming JSON response
            ctx.Response.ContentType = "application/json";

            var chatResponse = await chatClient.GetResponseAsync(chatMessages, cancellationToken: ct);
            var replyText    = chatResponse.Text ?? string.Empty;

            var completion = new OaiCompletion(
                Id:      responseId,
                Object:  "chat.completion",
                Created: created,
                Model:   agentId,
                Choices: [new OaiCompletionChoice(0, new OaiMessage("assistant", replyText), "stop")],
                Usage:   new OaiUsage(0, 0, 0));

            await ctx.Response.WriteAsync(
                JsonSerializer.Serialize(completion, OaiJsonContext.Default.OaiCompletion), ct);

            session.History.Add(new OaiMessage("assistant", replyText));
        }

        // --- Persist updated session
        await sessionStore.SaveAsync(session, ct);
    }

    private async Task HandleResponsesAsync(HttpContext ctx, CancellationToken ct)
    {
        using var doc = await JsonDocument.ParseAsync(ctx.Request.Body, cancellationToken: ct);
        var root = doc.RootElement;

        if (!root.TryGetProperty("input", out var inputElement))
        {
            ctx.Response.StatusCode = 400;
            return;
        }

        var userText = inputElement.ValueKind switch
        {
            JsonValueKind.String => inputElement.GetString() ?? string.Empty,
            JsonValueKind.Array  => ExtractUserText(inputElement),
            _                    => string.Empty
        };

        var isStreaming = root.TryGetProperty("stream", out var streamProp)
            && streamProp.ValueKind == JsonValueKind.True;

        var chatMessages = new List<ChatMessage> { new(ChatRole.User, userText) };

        if (isStreaming)
        {
            ctx.Response.Headers["Content-Type"]  = "text/event-stream";
            ctx.Response.Headers["Cache-Control"] = "no-cache";

            var responseId = $"resp_{Guid.NewGuid():N}";
            var msgId      = $"msg_{Guid.NewGuid():N}";
            var created    = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var fullReply  = new StringBuilder();
            var seq        = 0;

            // response.created — status in_progress, empty output
            var inProgressResponse = BuildResponsesResponse(responseId, "in_progress", created, msgId, string.Empty);
            await WriteSseEventAsync(ctx, "response.created",
                JsonSerializer.Serialize(new OaiResponsesCreatedEvent("response.created", seq++, inProgressResponse),
                    OaiJsonContext.Default.OaiResponsesCreatedEvent), ct);

            // response.output_text.delta — one event per streaming chunk
            await foreach (var update in chatClient.GetStreamingResponseAsync(chatMessages, cancellationToken: ct))
            {
                var delta = update.Text ?? string.Empty;
                fullReply.Append(delta);

                await WriteSseEventAsync(ctx, "response.output_text.delta",
                    JsonSerializer.Serialize(new OaiResponsesOutputTextDeltaEvent(
                        "response.output_text.delta", seq++, msgId, 0, 0, delta),
                        OaiJsonContext.Default.OaiResponsesOutputTextDeltaEvent), ct);
            }

            var fullText = fullReply.ToString();

            // response.output_text.done — assembled text
            await WriteSseEventAsync(ctx, "response.output_text.done",
                JsonSerializer.Serialize(new OaiResponsesOutputTextDoneEvent(
                    "response.output_text.done", seq++, msgId, 0, 0, fullText),
                    OaiJsonContext.Default.OaiResponsesOutputTextDoneEvent), ct);

            // response.completed — final response with status=completed and full output
            var completedResponse = BuildResponsesResponse(responseId, "completed", created, msgId, fullText);
            await WriteSseEventAsync(ctx, "response.completed",
                JsonSerializer.Serialize(new OaiResponsesCompletedEvent("response.completed", seq++, completedResponse),
                    OaiJsonContext.Default.OaiResponsesCompletedEvent), ct);
        }
        else
        {
            var chatResponse = await chatClient.GetResponseAsync(chatMessages, cancellationToken: ct);
            var replyText    = chatResponse.Text ?? string.Empty;

            var response = BuildResponsesResponse(
                $"resp_{Guid.NewGuid():N}", "completed",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                $"msg_{Guid.NewGuid():N}", replyText);

            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(
                JsonSerializer.Serialize(response, OaiJsonContext.Default.OaiResponsesResponse), ct);
        }
    }

    private OaiResponsesResponse BuildResponsesResponse(
        string responseId, string status, long createdAt, string msgId, string text)
        => new(
            Id:        responseId,
            Object:    "response",
            Status:    status,
            CreatedAt: createdAt,
            Model:     agentId,
            Output:   [new OaiResponseOutputMessage(
                Type:    "message",
                Id:      msgId,
                Role:    "assistant",
                Content: [new OaiResponseOutputContent(
                    Type:        "output_text",
                    Text:        text,
                    Annotations: [],
                    Logprobs:    [])])]);

    private static async Task WriteSseEventAsync(HttpContext ctx, string eventName, string data, CancellationToken ct)
    {
        await ctx.Response.WriteAsync($"event: {eventName}\ndata: {data}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }

    // Walks the input array (reverse order) to find the last user message text.
    private static string ExtractUserText(JsonElement inputArray)
    {
        foreach (var item in inputArray.EnumerateArray().Reverse())
        {
            if (!item.TryGetProperty("role", out var role) || role.GetString() != "user")
                continue;

            if (!item.TryGetProperty("content", out var content))
                continue;

            if (content.ValueKind == JsonValueKind.String)
                return content.GetString() ?? string.Empty;

            if (content.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in content.EnumerateArray())
                {
                    if (part.TryGetProperty("type", out var type) && type.GetString() == "input_text"
                        && part.TryGetProperty("text", out var text))
                        return text.GetString() ?? string.Empty;
                }
            }
        }
        return string.Empty;
    }

    // Convenience: build and run the server from just an IChatClient
    public static WebApplication Build(
        IChatClient chatClient,
        string agentId,
        int port,
        ISessionStore? sessionStore = null)
    {
        var store  = sessionStore ?? new LocalFileSessionStore();
        var server = new OaiServer(chatClient, store, agentId);

        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, OaiJsonContext.Default));

        // Suppress ASP.NET Core infrastructure logs — the caller (forge serve)
        // prints its own startup banner and handles errors with clean messages.
        builder.Logging.AddFilter("Microsoft", LogLevel.None);
        builder.Logging.AddFilter("System", LogLevel.None);

        builder.WebHost.UseSetting("urls", $"http://0.0.0.0:{port}");

        var app = builder.Build();
        server.Map(app);
        return app;
    }
}
