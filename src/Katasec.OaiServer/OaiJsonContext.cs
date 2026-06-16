using System.Text.Json.Serialization;

namespace Katasec.OaiServer;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OaiRequest))]
[JsonSerializable(typeof(OaiMessage))]
[JsonSerializable(typeof(OaiChunk))]
[JsonSerializable(typeof(OaiChoice))]
[JsonSerializable(typeof(OaiDelta))]
[JsonSerializable(typeof(Session))]
[JsonSerializable(typeof(List<OaiMessage>))]
internal partial class OaiJsonContext : JsonSerializerContext { }
