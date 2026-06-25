using Microsoft.Extensions.AI;

namespace Katasec.OaiServer.Tests.Infrastructure;

/// <summary>
/// Returns a fixed response so tests run offline without hitting any LLM provider.
/// </summary>
internal sealed class StubChatClient(string reply = "stub response") : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new ChatResponseUpdate { Contents = [new TextContent(reply)] };
        await Task.CompletedTask;
    }

    public ChatClientMetadata Metadata => new("stub", null, null);

    public object? GetService(Type serviceType, object? key = null) => null;

    public void Dispose() { }
}
