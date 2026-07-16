using System.Text.Json.Serialization;

namespace Katasec.AnthropicServer;

[JsonSerializable(typeof(System.Text.Json.JsonElement))]
[JsonSerializable(typeof(AnthropicRequest))]
[JsonSerializable(typeof(AnthropicMessage))]
[JsonSerializable(typeof(List<AnthropicMessage>))]
[JsonSerializable(typeof(AnthropicResponse))]
[JsonSerializable(typeof(AnthropicContentBlock))]
[JsonSerializable(typeof(List<AnthropicContentBlock>))]
[JsonSerializable(typeof(AnthropicUsage))]
[JsonSerializable(typeof(AnthropicMessageStart))]
[JsonSerializable(typeof(AnthropicMessageStartPayload))]
[JsonSerializable(typeof(AnthropicContentBlockStart))]
[JsonSerializable(typeof(AnthropicPing))]
[JsonSerializable(typeof(AnthropicContentBlockDelta))]
[JsonSerializable(typeof(AnthropicTextDelta))]
[JsonSerializable(typeof(AnthropicContentBlockStop))]
[JsonSerializable(typeof(AnthropicMessageDelta))]
[JsonSerializable(typeof(AnthropicMessageDeltaPayload))]
[JsonSerializable(typeof(AnthropicMessageStop))]
public partial class AnthropicJsonContext : JsonSerializerContext { }
