using Umbraco.Automate.Core.Settings;

namespace Umbraco.Forms.Automate.Actions;

/// <summary>
/// Settings for the <see cref="DeleteEntryAction"/>.
/// </summary>
/// <remarks>
/// Both settings are optional: left blank, the action deletes the entry that started the run, read
/// from the form trigger's output. That's what lets one step serve a trigger that matches several
/// forms, or all of them; picking a form is only needed to delete an entry from a specific form.
/// </remarks>
public sealed class DeleteEntrySettings
{
    /// <summary>
    /// Gets or sets the ID of the form the entry belongs to. When blank, the form trigger's form ID is used.
    /// </summary>
    [Field(
        Label = "Form",
        Description = "The form the entry belongs to. Leave blank to use the form of the entry that started the run.",
        EditorUiAlias = "Forms.PropertyEditorUi.FormPicker.Single",
        SupportsBindings = true)]
    public string? FormId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the entry to delete. When blank, the form trigger's record unique ID is used.
    /// </summary>
    [Field(
        Label = "Entry ID",
        Description = "The unique identifier of the entry to delete. Leave blank to delete the entry that started the run. Supports ${ binding } syntax.",
        SortOrder = 1,
        SupportsBindings = true)]
    public string? RecordUniqueId { get; set; }
}
