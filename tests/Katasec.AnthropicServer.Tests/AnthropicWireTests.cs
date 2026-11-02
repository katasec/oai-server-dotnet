using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Katasec.Anthropic.Models;

namespace Katasec.AnthropicServer.Tests;

public class AnthropicWireTests
{
    // Pins: what the server writes must not change when the types gain optional fields.
    [Fact]
    public void Response_with_text_and_tool_use_blocks()
    {
        var response = new AnthropicResponse
        {
            Id         = "msg_1",
            Model      = "m",
            Content    =
            [
                new AnthropicContentBlock { Type = "text", Text = "hi" },
                new AnthropicContentBlock
                {
                    Type = "tool_use", Text = null, Id = "toolu_1", Name = "Read", Input = Parse("""{"file_path":"/a"}"""),
                },
            ],
            StopReason = "tool_use",
            Usage      = new AnthropicUsage(),
        };

        AssertJson(
            """{"id":"msg_1","type":"message","role":"assistant","model":"m","content":[{"type":"text","text":"hi"},{"type":"tool_use","id":"toolu_1","name":"Read","input":{"file_path":"/a"}}],"stop_reason":"tool_use","stop_sequence":null,"usage":{"input_tokens":0,"output_tokens":0}}""",
            response, AnthropicJsonContext.Default.AnthropicResponse);
    }

    [Fact]
    public void Message_start_with_id_and_model()
        => AssertJson(
            """{"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","model":"m","content":[],"stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":0,"output_tokens":0}}}""",
            new AnthropicMessageStart { Message = new AnthropicMessageStartPayload { Id = "msg_1", Model = "m" } },
            AnthropicJsonContext.Default.AnthropicMessageStart);

    [Fact]
    public void Message_delta_with_stop_reason_and_output_tokens()
        => AssertJson(
            """{"type":"message_delta","delta":{"stop_reason":"tool_use","stop_sequence":null},"usage":{"input_tokens":0,"output_tokens":5}}""",
            new AnthropicMessageDelta
            {
                Delta = new AnthropicMessageDeltaPayload { StopReason = "tool_use" },
                Usage = new AnthropicUsage { OutputTokens = 5 },
            },
            AnthropicJsonContext.Default.AnthropicMessageDelta);

    [Fact]
    public void Request_without_tools_key_maps_no_tools()
    {
        var request = JsonSerializer.Deserialize(
            """{"model":"m","messages":[{"role":"user","content":"hi"}]}""",
            AnthropicJsonContext.Default.AnthropicRequest);

        Assert.NotNull(request);
        Assert.Empty(ToolMapping.MapDeclaredTools(request));
    }

    [Fact]
    public void Request_without_optional_fields_writes_only_required_keys()
        => AssertJson(
            """{"model":"m","messages":[],"max_tokens":1024,"stream":false}""",
            new AnthropicRequest { Model = "m" }, AnthropicJsonContext.Default.AnthropicRequest);

    [Fact]
    public void Request_with_optional_fields_writes_snake_case_keys()
        => AssertJson(
            """{"model":"m","messages":[],"max_tokens":1024,"stream":false,"tool_choice":{"type":"auto","disable_parallel_tool_use":true},"stop_sequences":["END"],"temperature":0.5,"top_p":0.9,"top_k":40}""",
            new AnthropicRequest
            {
                Model         = "m",
                ToolChoice    = new AnthropicToolChoice { Type = "auto", DisableParallelToolUse = true },
                StopSequences = ["END"],
                Temperature   = 0.5f,
                TopP          = 0.9f,
                TopK          = 40,
            },
            AnthropicJsonContext.Default.AnthropicRequest);

    [Fact]
    public void Tool_result_block_writes_exactly_its_four_keys()
        => AssertJson(
            """{"type":"tool_result","tool_use_id":"toolu_1","content":"file not found","is_error":true}""",
            new AnthropicContentBlock
            {
                Type = "tool_result", Text = null, ToolUseId = "toolu_1", Content = Parse("\"file not found\""), IsError = true,
            },
            AnthropicJsonContext.Default.AnthropicContentBlock);

    [Fact]
    public void Usage_reads_both_cache_counts()
    {
        var usage = JsonSerializer.Deserialize(
            """{"input_tokens":10,"output_tokens":2,"cache_creation_input_tokens":3,"cache_read_input_tokens":4}""",
            AnthropicJsonContext.Default.AnthropicUsage);

        Assert.NotNull(usage);
        Assert.Equal(3, usage.CacheCreationInputTokens);
        Assert.Equal(4, usage.CacheReadInputTokens);
    }

    [Fact]
    public void Error_body_with_request_id_reads_type_and_message()
    {
        var body = JsonSerializer.Deserialize(
            """{"type":"error","error":{"type":"invalid_request_error","message":"prompt is too long: 1 tokens > 0 maximum"},"request_id":"req_1"}""",
            AnthropicJsonContext.Default.AnthropicErrorResponse);

        Assert.NotNull(body);
        Assert.Equal("invalid_request_error", body.Error.Type);
        Assert.Equal("prompt is too long: 1 tokens > 0 maximum", body.Error.Message);
    }

    private static void AssertJson<T>(string expected, T value, JsonTypeInfo<T> typeInfo)
        => Assert.Equal(expected, JsonSerializer.Serialize(value, typeInfo));

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
