using System.Text.Json.Serialization;

namespace Katasec.OaiServer;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OaiRequest))]
[JsonSerializable(typeof(OaiMessage))]
[JsonSerializable(typeof(OaiCompletion))]
[JsonSerializable(typeof(OaiCompletionChoice))]
[JsonSerializable(typeof(List<OaiCompletionChoice>))]
[JsonSerializable(typeof(OaiUsage))]
[JsonSerializable(typeof(OaiChunk))]
[JsonSerializable(typeof(OaiChoice))]
[JsonSerializable(typeof(OaiDelta))]
[JsonSerializable(typeof(Session))]
[JsonSerializable(typeof(List<OaiMessage>))]
[JsonSerializable(typeof(OaiModelsListResponse))]
[JsonSerializable(typeof(OaiModelInfo))]
[JsonSerializable(typeof(List<OaiModelInfo>))]
internal partial class OaiJsonContext : JsonSerializerContext { }
