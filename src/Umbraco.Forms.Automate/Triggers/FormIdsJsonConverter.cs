using System.Text.Json;
using System.Text.Json.Serialization;

namespace Umbraco.Forms.Automate.Triggers;

/// <summary>
/// Reads the trigger's stored form-picker value into distinct form ids, whatever shape it
/// was persisted in.
/// </summary>
/// <remarks>
/// <para>
/// The picker (<c>Forms.PropertyEditorUi.FormPicker.Multiple</c>) persists a JSON array —
/// <c>["id", "id"]</c> — but the value has also been observed as a single id and as a
/// comma-separated string, so all three shapes are accepted.
/// </para>
/// <para>
/// Unparseable tokens are dropped rather than thrown on. Trigger settings are resolved on the
/// dispatch path, where an exception is not contained to the offending automation: it stops
/// every automation using a form trigger (umbraco/Umbraco.Automate#219).
/// </para>
/// </remarks>
internal sealed class FormIdsJsonConverter : JsonConverter<IReadOnlyList<Guid>>
{
    private static readonly char[] Separators = [',', '[', ']', '"', ' '];

    /// <inheritdoc />
    public override IReadOnlyList<Guid> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var ids = new List<Guid>();

        if (reader.TokenType == JsonTokenType.String)
        {
            AddIds(reader.GetString(), ids);
            return ids;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return ids;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                AddIds(reader.GetString(), ids);
                continue;
            }

            // A nested array or object is not a form id — step over it and keep reading.
            reader.Skip();
        }

        return ids;
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        IReadOnlyList<Guid> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var id in value)
        {
            writer.WriteStringValue(id);
        }

        writer.WriteEndArray();
    }

    private static void AddIds(string? value, List<Guid> ids)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var token in value.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(token, out var id) && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }
    }
}
