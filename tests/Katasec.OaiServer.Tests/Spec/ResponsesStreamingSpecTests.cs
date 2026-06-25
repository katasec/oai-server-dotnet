#pragma warning disable OPENAI001

using Katasec.OaiServer.Tests.Infrastructure;
using OpenAI.Responses;
using System.ClientModel;

namespace Katasec.OaiServer.Tests.Spec;

/// <summary>
/// Spec-compliance tests for POST /v1/responses with stream=true.
/// Uses the official OpenAI .NET SDK's CreateResponseStreamingAsync.
/// Expected SSE format (from openai/openai-dotnet session records):
///   event: response.created\ndata: {...}\n\n
///   event: response.output_text.delta\ndata: {"delta":"Hello",...}\n\n
///   event: response.output_text.done\ndata: {"text":"Hello from forge.",...}\n\n
///   event: response.completed\ndata: {"response":{...}}\n\n
/// Assertions mirror StreamingResponses() in openai/openai-dotnet/tests/Responses/ResponsesTests.cs.
/// </summary>
public sealed class ResponsesStreamingSpecTests : IAsyncLifetime
{
    private OaiServerHarness _harness = null!;
    private ResponsesClient _client = null!;

    public async Task InitializeAsync()
    {
        _harness = await OaiServerHarness.StartAsync(stubReply: "Hello from forge.");
        _client  = new ResponsesClient(
            credential: new ApiKeyCredential("fake-key"),
            options:    new ResponsesClientOptions { Endpoint = new Uri(_harness.BaseUrl) });
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    private CreateResponseOptions StreamingRequest() => new(
        _harness.AgentId,
        [ResponseItem.CreateUserMessageItem("Hello")])
    {
        StreamingEnabled = true
    };

    [Fact]
    public async Task Streaming_ProducesAtLeastOneUpdate()
    {
        var updates = new List<StreamingResponseUpdate>();

        await foreach (var update in _client.CreateResponseStreamingAsync(StreamingRequest()))
            updates.Add(update);

        Assert.NotEmpty(updates);
    }

    [Fact]
    public async Task Streaming_ProducesDeltaTextUpdates()
    {
        var deltas = new List<string>();

        await foreach (var update in _client.CreateResponseStreamingAsync(StreamingRequest()))
        {
            if (update is StreamingResponseOutputTextDeltaUpdate delta)
                deltas.Add(delta.Delta);
        }

        Assert.NotEmpty(deltas);
    }

    [Fact]
    public async Task Streaming_DeltaText_AssemblesIntoNonEmptyString()
    {
        var assembled = new System.Text.StringBuilder();

        await foreach (var update in _client.CreateResponseStreamingAsync(StreamingRequest()))
        {
            if (update is StreamingResponseOutputTextDeltaUpdate delta)
                assembled.Append(delta.Delta);
        }

        Assert.False(string.IsNullOrEmpty(assembled.ToString()));
    }

    [Fact]
    public async Task Streaming_OutputTextDone_HasFullText()
    {
        string? doneText = null;

        await foreach (var update in _client.CreateResponseStreamingAsync(StreamingRequest()))
        {
            if (update is StreamingResponseOutputTextDoneUpdate done)
                doneText = done.Text;
        }

        Assert.False(string.IsNullOrEmpty(doneText));
    }

    [Fact]
    public async Task Streaming_CompletedUpdate_ResponseStatusIsCompleted()
    {
        StreamingResponseCompletedUpdate? completed = null;

        await foreach (var update in _client.CreateResponseStreamingAsync(StreamingRequest()))
        {
            if (update is StreamingResponseCompletedUpdate c)
                completed = c;
        }

        Assert.NotNull(completed);
        Assert.Equal(ResponseStatus.Completed, completed!.Response.Status);
    }

    [Fact]
    public async Task Streaming_CompletedUpdate_ResponseOutputTextIsPresent()
    {
        StreamingResponseCompletedUpdate? completed = null;

        await foreach (var update in _client.CreateResponseStreamingAsync(StreamingRequest()))
        {
            if (update is StreamingResponseCompletedUpdate c)
                completed = c;
        }

        Assert.NotNull(completed);
        Assert.False(string.IsNullOrEmpty(completed!.Response.GetOutputText()));
    }
}
