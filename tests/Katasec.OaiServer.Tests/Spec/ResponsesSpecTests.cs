#pragma warning disable OPENAI001

using Katasec.OaiServer.Tests.Infrastructure;
using OpenAI.Responses;
using System.ClientModel;

namespace Katasec.OaiServer.Tests.Spec;

/// <summary>
/// Spec-compliance tests for POST /v1/responses (OpenAI Responses API, 2025).
/// This endpoint is not yet implemented in OaiServer — all tests here are
/// expected to fail until the /v1/responses route is added.
/// </summary>
public sealed class ResponsesSpecTests : IAsyncLifetime
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

    [Fact]
    public async Task CreateResponse_ReturnsValidResponse()
    {
        ClientResult<ResponseResult> result = await _client.CreateResponseAsync(
            new CreateResponseOptions(_harness.AgentId, [ResponseItem.CreateUserMessageItem("Hello")]));

        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task CreateResponse_Id_IsPresent()
    {
        ClientResult<ResponseResult> result = await _client.CreateResponseAsync(
            new CreateResponseOptions(_harness.AgentId, [ResponseItem.CreateUserMessageItem("Hello")]));

        Assert.False(string.IsNullOrEmpty(result.Value.Id));
    }

    [Fact]
    public async Task CreateResponse_OutputText_IsPresent()
    {
        ClientResult<ResponseResult> result = await _client.CreateResponseAsync(
            new CreateResponseOptions(_harness.AgentId, [ResponseItem.CreateUserMessageItem("Hello")]));

        Assert.False(string.IsNullOrEmpty(result.Value.GetOutputText()));
    }

    [Fact]
    public async Task CreateResponse_Status_IsCompleted()
    {
        ClientResult<ResponseResult> result = await _client.CreateResponseAsync(
            new CreateResponseOptions(_harness.AgentId, [ResponseItem.CreateUserMessageItem("Hello")]));

        Assert.Equal(ResponseStatus.Completed, result.Value.Status);
    }
}
