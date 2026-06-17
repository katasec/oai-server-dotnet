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

            await foreach (var update in chatClient.GetStreamingResponseAsync(chatMessages, cancellationToken: ct))
            {
                var text = update.Text ?? string.Empty;
                fullReply.Append(text);

                var chunk = new OaiChunk(
                    Id:      responseId,
                    Object:  "chat.completion.chunk",
                    Created: created,
                    Model:   agentId,
                    Choices: [new OaiChoice(0, new OaiDelta(null, text), null)]);

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
