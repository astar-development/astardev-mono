using System.Text.Json;
using System.Text.Json.Serialization;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Wire shape of one frame. Commands, queries and events travel in separate envelope types,
/// told apart by the "kind" discriminator. Payloads stay raw JSON until a handler knows the type.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = ApplicationConstants.EnvelopeKindDiscriminator)]
[JsonDerivedType(typeof(CommandEnvelope), ApplicationConstants.CommandKind)]
[JsonDerivedType(typeof(QueryEnvelope), ApplicationConstants.QueryKind)]
[JsonDerivedType(typeof(SubscribeEnvelope), ApplicationConstants.SubscribeKind)]
[JsonDerivedType(typeof(ResponseEnvelope), ApplicationConstants.ResponseKind)]
[JsonDerivedType(typeof(ErrorEnvelope), ApplicationConstants.ErrorKind)]
[JsonDerivedType(typeof(EventEnvelope), ApplicationConstants.EventKind)]
internal abstract record Envelope;

/// <summary>Client request, answered by a <see cref="ResponseEnvelope"/> or an <see cref="ErrorEnvelope"/> with the same id.</summary>
internal abstract record RequestEnvelope(long Id) : Envelope;

internal sealed record CommandEnvelope(long Id, string Type, JsonElement Payload) : RequestEnvelope(Id);

internal sealed record QueryEnvelope(long Id, string Type, JsonElement Payload) : RequestEnvelope(Id);

internal sealed record SubscribeEnvelope(long Id) : RequestEnvelope(Id);

internal sealed record ResponseEnvelope(long Id, JsonElement Payload) : Envelope;

internal sealed record ErrorEnvelope(long Id, string Message) : Envelope;

internal sealed record EventEnvelope(string Type, JsonElement Payload) : Envelope;

/// <summary>Every call here can throw on bad input, so each is wrapped and returned as a <see cref="Result{TResult, TError}"/>.</summary>
internal static class IpcJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static Result<JsonElement, IpcError> ToElement(object? value)
        => Try.Run(() => JsonSerializer.SerializeToElement(value, value?.GetType() ?? typeof(object), Options))
            .ToResult(IpcErrors.AsMalformed);

    public static Result<T, IpcError> FromElement<T>(JsonElement element)
        => Try.Run(() => element.Deserialize<T>(Options) ?? throw new JsonException($"Expected a {typeof(T).Name} but the payload was null."))
            .ToResult(IpcErrors.AsMalformed);

    public static Result<byte[], IpcError> Encode(Envelope envelope)
        => Try.Run(() => JsonSerializer.SerializeToUtf8Bytes(envelope, Options))
            .ToResult(IpcErrors.AsMalformed);

    public static Result<Envelope, IpcError> Decode(byte[] bytes)
        => Try.Run(() => JsonSerializer.Deserialize<Envelope>(bytes, Options) ?? throw new JsonException("The frame holds JSON null instead of an envelope."))
            .ToResult(IpcErrors.AsMalformed);
}
