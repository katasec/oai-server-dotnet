using System.Net;
using Microsoft.AspNetCore.Builder;

namespace Katasec.OaiServer.Tests.Infrastructure;

/// <summary>
/// Starts OaiServer in-process on a random port and exposes an HttpClient for tests.
/// </summary>
internal sealed class OaiServerHarness : IAsyncDisposable
{
    private readonly WebApplication _app;

    public HttpClient HttpClient { get; }
    public string AgentId { get; } = "test-model";
    public string BaseUrl { get; }

    private OaiServerHarness(WebApplication app, HttpClient httpClient, string baseUrl)
    {
        _app = app;
        HttpClient = httpClient;
        BaseUrl = baseUrl;
    }

    public static async Task<OaiServerHarness> StartAsync(string? agentId = null, string? stubReply = null)
    {
        var port = GetFreePort();
        var chatClient = new StubChatClient(stubReply ?? "stub response");
        var id = agentId ?? "test-model";

        var app = OaiServer.Build(chatClient, id, port, new InMemorySessionStore());
        await app.StartAsync();

        var httpClient = new HttpClient();
        var baseUrl = $"http://localhost:{port}/v1";
        return new OaiServerHarness(app, httpClient, baseUrl);
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        HttpClient.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
