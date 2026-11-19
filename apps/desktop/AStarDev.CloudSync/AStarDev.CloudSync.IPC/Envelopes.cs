using System.Text.Json;
using System.Text.Json.Serialization;

namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Wire shape of one frame. Commands, queries and events travel in separate envelope types,
/// told apart by the "kind" discriminator. Payloads stay raw JSON until a handler knows the type.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(CommandEnvelope), "command")]
[JsonDerivedType(typeof(QueryEnvelope), "query")]
[JsonDerivedType(typeof(SubscribeEnvelope), "subscribe")]
[JsonDerivedType(typeof(ResponseEnvelope), "response")]
[JsonDerivedType(typeof(EventEnvelope), "event")]
internal abstract record Envelope;

/// <summary>Client request, answered by a <see cref="ResponseEnvelope"/> with the same id.</summary>
internal abstract record RequestEnvelope(long Id) : Envelope;

internal sealed record CommandEnvelope(long Id, string Type, JsonElement Payload) : RequestEnvelope(Id);

internal sealed record QueryEnvelope(long Id, string Type, JsonElement Payload) : RequestEnvelope(Id);

internal sealed record SubscribeEnvelope(long Id) : RequestEnvelope(Id);

internal sealed record ResponseEnvelope(long Id, JsonElement? Payload, string? Error) : Envelope;

internal sealed record EventEnvelope(string Type, JsonElement Payload) : Envelope;

internal static class IpcJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static JsonElement ToElement(object value) => JsonSerializer.SerializeToElement(value, value.GetType(), Options);

    public static T FromElement<T>(JsonElement element)
        => element.Deserialize<T>(Options) ?? throw new JsonException($"Expected a {typeof(T).Name} but the payload was null.");
}
