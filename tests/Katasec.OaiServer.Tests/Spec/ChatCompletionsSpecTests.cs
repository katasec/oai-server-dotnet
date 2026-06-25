using Katasec.OaiServer.Tests.Infrastructure;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace Katasec.OaiServer.Tests.Spec;

/// <summary>
/// Spec-compliance tests for POST /v1/chat/completions (non-streaming).
/// Uses the official OpenAI .NET SDK pointed at the in-process server.
/// If the real SDK can call our server and deserialize the response, any
/// OpenAI-compatible client will work.
/// </summary>
public sealed class ChatCompletionsSpecTests : IAsyncLifetime
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
    public async Task NonStreaming_ReturnsValidChatCompletion()
    {
        ChatCompletion completion = await _client.CompleteChatAsync(
            [new UserChatMessage("Hello")]);

        Assert.NotNull(completion);
    }

    [Fact]
    public async Task NonStreaming_Id_IsPresent()
    {
        ChatCompletion completion = await _client.CompleteChatAsync(
            [new UserChatMessage("Hello")]);

        Assert.False(string.IsNullOrEmpty(completion.Id));
    }

    [Fact]
    public async Task NonStreaming_Model_IsPresent()
    {
        ChatCompletion completion = await _client.CompleteChatAsync(
            [new UserChatMessage("Hello")]);

        Assert.False(string.IsNullOrEmpty(completion.Model));
    }

    [Fact]
    public async Task NonStreaming_CreatedAt_IsPresent()
    {
        ChatCompletion completion = await _client.CompleteChatAsync(
            [new UserChatMessage("Hello")]);

        Assert.True(completion.CreatedAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task NonStreaming_Choices_ContainsAtLeastOne()
    {
        ChatCompletion completion = await _client.CompleteChatAsync(
            [new UserChatMessage("Hello")]);

        Assert.NotEmpty(completion.Content);
    }

    [Fact]
    public async Task NonStreaming_FinishReason_IsStop()
    {
        ChatCompletion completion = await _client.CompleteChatAsync(
            [new UserChatMessage("Hello")]);

        Assert.Equal(ChatFinishReason.Stop, completion.FinishReason);
    }

    [Fact]
    public async Task NonStreaming_Content_IsNotNull()
    {
        ChatCompletion completion = await _client.CompleteChatAsync(
            [new UserChatMessage("Hello")]);

        Assert.NotNull(completion.Content[0].Text);
    }

    [Fact]
    public async Task NonStreaming_Usage_IsPresent()
    {
        ChatCompletion completion = await _client.CompleteChatAsync(
            [new UserChatMessage("Hello")]);

        Assert.NotNull(completion.Usage);
    }
}
