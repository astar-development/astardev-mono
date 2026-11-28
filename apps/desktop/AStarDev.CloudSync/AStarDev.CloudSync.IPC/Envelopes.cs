using System.Text.Json;
using System.Text.Json.Serialization;
using AStarDev.CloudSync.Errors;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Wire shape of one frame. Commands, queries and events travel in separate envelope types,
/// told apart by the "kind" discriminator. A payload is written once by its runtime type and read back as raw JSON until a handler knows the type.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = ApplicationConstants.EnvelopeKindDiscriminator)]
[JsonDerivedType(typeof(CommandEnvelope), ApplicationConstants.CommandKind)]
[JsonDerivedType(typeof(QueryEnvelope), ApplicationConstants.QueryKind)]
[JsonDerivedType(typeof(SubscribeEnvelope), ApplicationConstants.SubscribeKind)]
[JsonDerivedType(typeof(ResponseEnvelope), ApplicationConstants.ResponseKind)]
[JsonDerivedType(typeof(ErrorEnvelope), ApplicationConstants.ErrorKind)]
[JsonDerivedType(typeof(EventEnvelope), ApplicationConstants.EventKind)]
internal abstract record Envelope;

/// <summary>An envelope that can carry the W3C trace context of the span that sent it, so the receiving span joins the same trace. Both fields are omitted from the wire when empty.</summary>
internal abstract record TracedEnvelope : Envelope
{
    /// <summary>The sender's span as a W3C <c>traceparent</c> value. <see langword="null"/> when the sender is not being traced.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Traceparent { get; init; }

    /// <summary>The W3C <c>tracestate</c> value that goes with <see cref="Traceparent"/>. <see langword="null"/> when there is none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tracestate { get; init; }
}

/// <summary>Client request, answered by a <see cref="ResponseEnvelope"/> or an <see cref="ErrorEnvelope"/> with the same id.</summary>
internal abstract record RequestEnvelope(long Id) : TracedEnvelope;

/// <summary>Carries an <see cref="ICommand"/>. <paramref name="Type"/> is the command's type name, which picks the server handler.</summary>
internal sealed record CommandEnvelope(long Id, string Type, [property: JsonRequired, JsonConverter(typeof(PayloadJsonConverter))] object Payload) : RequestEnvelope(Id);

/// <summary>Carries an <see cref="IQuery{TResult}"/>. <paramref name="Type"/> is the query's type name, which picks the server handler.</summary>
internal sealed record QueryEnvelope(long Id, string Type, [property: JsonRequired, JsonConverter(typeof(PayloadJsonConverter))] object Payload) : RequestEnvelope(Id);

/// <summary>Asks the server to push every later <see cref="EventEnvelope"/> to this connection. Carries no payload.</summary>
internal sealed record SubscribeEnvelope(long Id) : RequestEnvelope(Id);

/// <summary>Successful answer to the request with the same <paramref name="Id"/>. <paramref name="Payload"/> is the handler's result.</summary>
internal sealed record ResponseEnvelope(long Id, [property: JsonRequired, JsonConverter(typeof(PayloadJsonConverter))] object Payload) : Envelope;

/// <summary>Failed answer to the request with the same <paramref name="Id"/>. The client surfaces <paramref name="Message"/> as <see cref="HandlerFailed"/>, keeping <paramref name="Code"/>, the <c>CS-IPC-NNNN</c> code of the error the server hit, as <see cref="HandlerFailed.Reported"/>.</summary>
internal sealed record ErrorEnvelope(long Id, string Message, string Code) : Envelope;

/// <summary>Server push to subscribed connections. Has no id because nothing answers it; <paramref name="Type"/> picks the handlers registered with <see cref="IIpcClient.OnEvent{TEvent}"/>.</summary>
internal sealed record EventEnvelope(string Type, [property: JsonRequired, JsonConverter(typeof(PayloadJsonConverter))] object Payload) : TracedEnvelope;

/// <summary>Every call here can throw on bad input, so each is wrapped and returned as a <see cref="Result{TResult, TError}"/>.</summary>
internal static class IpcJson
{
    /// <summary>Web defaults with enums as strings. Both peers use these, so they agree on the wire format.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>A JSON <c>null</c> payload, sent in place of a handler result that is <see langword="null"/>, so the receiver reports it as a <see cref="MalformedFrame"/>.</summary>
    public static readonly JsonElement NullPayload = JsonSerializer.SerializeToElement<object?>(null);

    /// <summary>Serialises by the runtime type of <paramref name="value"/>, so a value held as an interface keeps all its properties.</summary>
    public static Result<JsonElement, IpcError> ToElement(object? value)
        => Try.Run(() => JsonSerializer.SerializeToElement(value, value?.GetType() ?? typeof(object), Options))
            .ToResult(IpcErrors.AsMalformed);

    /// <summary>Reads a payload as <typeparamref name="T"/>. A received payload is a <see cref="JsonElement"/>; any other value is first serialised by its runtime type. A JSON <c>null</c> payload is a <see cref="MalformedFrame"/>, never a <see langword="null"/> value.</summary>
    public static Result<T, IpcError> FromElement<T>(object payload)
        => Try.Run(() => Element(payload).Deserialize<T>(Options) ?? throw new JsonException($"Expected a {typeof(T).Name} but the payload was null.").WithHelpLink(IpcErrorCodes.MalformedFrame))
            .ToResult(IpcErrors.AsMalformed);

    /// <summary>Produces the bytes of one frame payload, with the <c>kind</c> discriminator that <see cref="Decode"/> needs. The envelope's payload is serialised once, straight into the frame.</summary>
    public static Result<byte[], IpcError> Encode(Envelope envelope)
        => Try.Run(() => JsonSerializer.SerializeToUtf8Bytes(envelope, Options))
            .ToResult(IpcErrors.AsMalformed);

    /// <summary>Rebuilds the right <see cref="Envelope"/> subtype from its <c>kind</c> discriminator, leaving any payload as a <see cref="JsonElement"/>. JSON <c>null</c>, an unknown kind or a missing payload is a <see cref="MalformedFrame"/>.</summary>
    public static Result<Envelope, IpcError> Decode(byte[] bytes)
        => Try.Run(() => JsonSerializer.Deserialize<Envelope>(bytes, Options) ?? throw new JsonException("The frame holds JSON null instead of an envelope.").WithHelpLink(IpcErrorCodes.MalformedFrame))
            .ToResult(IpcErrors.AsMalformed);

    private static JsonElement Element(object payload) => payload is JsonElement element ? element : JsonSerializer.SerializeToElement(payload, payload.GetType(), Options);
}
