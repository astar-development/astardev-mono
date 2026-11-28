using System.Text.Json;
using System.Text.Json.Serialization;

namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Writes an envelope payload straight into the frame by its runtime type, so it is serialised once and a value held as an interface keeps all its properties.
/// Reads it back as a raw <see cref="JsonElement"/> until a handler knows the type; a JSON <c>null</c> payload is read as a <see cref="JsonValueKind.Null"/> element, never <see langword="null"/>.
/// </summary>
internal sealed class PayloadJsonConverter : JsonConverter<object>
{
    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <inheritdoc/>
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => JsonElement.ParseValue(ref reader);

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value, value.GetType(), options);
}
