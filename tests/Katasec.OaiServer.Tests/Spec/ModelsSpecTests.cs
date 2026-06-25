using Katasec.OaiServer.Tests.Infrastructure;
using OpenAI;
using OpenAI.Models;
using System.ClientModel;

namespace Katasec.OaiServer.Tests.Spec;

/// <summary>
/// Spec-compliance tests for GET /v1/models.
/// Uses the official OpenAI .NET SDK's OpenAIModelClient.
/// </summary>
public sealed class ModelsSpecTests : IAsyncLifetime
{
    private OaiServerHarness _harness = null!;
    private OpenAIModelClient _client = null!;

    public async Task InitializeAsync()
    {
        _harness = await OaiServerHarness.StartAsync();
        _client  = new OpenAIModelClient(
            credential: new ApiKeyCredential("fake-key"),
            options:    new OpenAIClientOptions { Endpoint = new Uri(_harness.BaseUrl) });
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task ListModels_ReturnsAtLeastOneModel()
    {
        OpenAIModelCollection models = await _client.GetModelsAsync();

        Assert.NotEmpty(models);
    }

    [Fact]
    public async Task ListModels_Model_HasId()
    {
        OpenAIModelCollection models = await _client.GetModelsAsync();

        Assert.False(string.IsNullOrEmpty(models.First().Id));
    }

    [Fact]
    public async Task ListModels_Model_IdMatchesAgentId()
    {
        OpenAIModelCollection models = await _client.GetModelsAsync();

        Assert.Equal(_harness.AgentId, models.First().Id);
    }

    [Fact]
    public async Task ListModels_Model_HasCreatedAt()
    {
        OpenAIModelCollection models = await _client.GetModelsAsync();

        Assert.True(models.First().CreatedAt > DateTimeOffset.MinValue);
    }
}
