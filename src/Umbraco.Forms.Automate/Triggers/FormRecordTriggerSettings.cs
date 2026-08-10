using System.Text.Json.Serialization;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Forms.Automate.Triggers;

/// <summary>
/// Settings shared by form record triggers (submitted, approved).
/// </summary>
public sealed class FormRecordTriggerSettings
{
    /// <summary>
    /// Gets or sets the form IDs to filter on. Leave blank to match all forms.
    /// </summary>
    /// <remarks>
    /// A collection, because the picker is a multiple picker: declaring this as a single value
    /// made Automate reject any selection of two or more forms outright
    /// (umbraco/Umbraco.Automate#219). <see cref="FormIdsJsonConverter"/> keeps the older
    /// single-value and comma-separated storage shapes readable.
    /// </remarks>
    [Field(
        Label = "Forms",
        Description = "Only fire for these forms. Leave blank to match all.",
        EditorUiAlias = "Forms.PropertyEditorUi.FormPicker.Multiple")]
    [JsonConverter(typeof(FormIdsJsonConverter))]
    public IReadOnlyList<Guid>? FormIds { get; set; }
}
