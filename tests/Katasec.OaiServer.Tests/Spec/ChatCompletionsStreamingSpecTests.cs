using Katasec.OaiServer.Tests.Infrastructure;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace Katasec.OaiServer.Tests.Spec;

/// <summary>
/// Spec-compliance tests for POST /v1/chat/completions with stream=true.
/// Uses the official OpenAI .NET SDK's streaming API.
/// Expected SSE format (from openai/openai-dotnet mock tests):
///   data: {"id":"...","object":"chat.completion.chunk","created":...,"model":"...","choices":[{"index":0,"delta":{"role":"assistant","content":""},"finish_reason":null}]}
///   data: {"id":"...","object":"chat.completion.chunk","created":...,"model":"...","choices":[{"index":0,"delta":{"content":"Hello"},"finish_reason":null}]}
///   data: [DONE]
/// </summary>
public sealed class ChatCompletionsStreamingSpecTests : IAsyncLifetime
{
    private OaiServerHarness _harness = null!;
    private ChatClient _client = null!;

    public async Task InitializeAsync()
    {
        _harness = await OaiServerHarness.StartAsync(stubReply: "Hello from forge.");
        _client  = new ChatClient(
            model:      _harness.AgentId,
            credential: new ApiKeyCredential("fake-key"),
            options:    new OpenAIClientOptions { Endpoint = new Uri(_harness.BaseUrl) });
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Streaming_ProducesAtLeastOneUpdate()
    {
        var updates = new List<StreamingChatCompletionUpdate>();

        await foreach (var update in _client.CompleteChatStreamingAsync(
            [new UserChatMessage("Hello")]))
        {
            updates.Add(update);
        }

        Assert.NotEmpty(updates);
    }

    [Fact]
    public async Task Streaming_FirstUpdate_HasRoleAssistant()
    {
        StreamingChatCompletionUpdate? first = null;

        await foreach (var update in _client.CompleteChatStreamingAsync(
            [new UserChatMessage("Hello")]))
        {
            first = update;
            break;
        }

        Assert.NotNull(first);
        Assert.Equal(ChatMessageRole.Assistant, first!.Role);
    }

    [Fact]
    public async Task Streaming_Updates_HaveConsistentId()
    {
        var ids = new HashSet<string>();

        await foreach (var update in _client.CompleteChatStreamingAsync(
            [new UserChatMessage("Hello")]))
        {
            if (!string.IsNullOrEmpty(update.CompletionId))
                ids.Add(update.CompletionId);
        }

        Assert.Single(ids);
    }

    [Fact]
    public async Task Streaming_ContentUpdates_ContainText()
    {
        var text = new System.Text.StringBuilder();

        await foreach (var update in _client.CompleteChatStreamingAsync(
            [new UserChatMessage("Hello")]))
        {
            foreach (var part in update.ContentUpdate)
                text.Append(part.Text);
        }

        Assert.False(string.IsNullOrEmpty(text.ToString()));
    }

    [Fact]
    public async Task Streaming_LastUpdate_HasStopFinishReason()
    {
        StreamingChatCompletionUpdate? last = null;

        await foreach (var update in _client.CompleteChatStreamingAsync(
            [new UserChatMessage("Hello")]))
        {
            last = update;
        }

        Assert.NotNull(last);
        Assert.Equal(ChatFinishReason.Stop, last!.FinishReason);
    }
}
