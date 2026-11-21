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

/// <summary>Carries an <see cref="ICommand"/>. <paramref name="Type"/> is the command's type name, which picks the server handler.</summary>
internal sealed record CommandEnvelope(long Id, string Type, JsonElement Payload) : RequestEnvelope(Id);

/// <summary>Carries an <see cref="IQuery{TResult}"/>. <paramref name="Type"/> is the query's type name, which picks the server handler.</summary>
internal sealed record QueryEnvelope(long Id, string Type, JsonElement Payload) : RequestEnvelope(Id);

/// <summary>Asks the server to push every later <see cref="EventEnvelope"/> to this connection. Carries no payload.</summary>
internal sealed record SubscribeEnvelope(long Id) : RequestEnvelope(Id);

/// <summary>Successful answer to the request with the same <paramref name="Id"/>. <paramref name="Payload"/> is the handler's result.</summary>
internal sealed record ResponseEnvelope(long Id, JsonElement Payload) : Envelope;

/// <summary>Failed answer to the request with the same <paramref name="Id"/>. The client surfaces <paramref name="Message"/> as <see cref="HandlerFailed"/>.</summary>
internal sealed record ErrorEnvelope(long Id, string Message) : Envelope;

/// <summary>Server push to subscribed connections. Has no id because nothing answers it; <paramref name="Type"/> picks the handlers registered with <see cref="IIpcClient.OnEvent{TEvent}"/>.</summary>
internal sealed record EventEnvelope(string Type, JsonElement Payload) : Envelope;

/// <summary>Every call here can throw on bad input, so each is wrapped and returned as a <see cref="Result{TResult, TError}"/>.</summary>
internal static class IpcJson
{
    /// <summary>Web defaults with enums as strings. Both peers use these, so they agree on the wire format.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Serialises by the runtime type of <paramref name="value"/>, so a value held as an interface keeps all its properties.</summary>
    public static Result<JsonElement, IpcError> ToElement(object? value)
        => Try.Run(() => JsonSerializer.SerializeToElement(value, value?.GetType() ?? typeof(object), Options))
            .ToResult(IpcErrors.AsMalformed);

    /// <summary>Reads a payload as <typeparamref name="T"/>. A JSON <c>null</c> payload is a <see cref="MalformedFrame"/>, never a <see langword="null"/> value.</summary>
    public static Result<T, IpcError> FromElement<T>(JsonElement element)
        => Try.Run(() => element.Deserialize<T>(Options) ?? throw new JsonException($"Expected a {typeof(T).Name} but the payload was null."))
            .ToResult(IpcErrors.AsMalformed);

    /// <summary>Produces the bytes of one frame payload, with the <c>kind</c> discriminator that <see cref="Decode"/> needs.</summary>
    public static Result<byte[], IpcError> Encode(Envelope envelope)
        => Try.Run(() => JsonSerializer.SerializeToUtf8Bytes(envelope, Options))
            .ToResult(IpcErrors.AsMalformed);

    /// <summary>Rebuilds the right <see cref="Envelope"/> subtype from its <c>kind</c> discriminator. JSON <c>null</c> or an unknown kind is a <see cref="MalformedFrame"/>.</summary>
    public static Result<Envelope, IpcError> Decode(byte[] bytes)
        => Try.Run(() => JsonSerializer.Deserialize<Envelope>(bytes, Options) ?? throw new JsonException("The frame holds JSON null instead of an envelope."))
            .ToResult(IpcErrors.AsMalformed);
}
