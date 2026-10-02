namespace Umbraco.Forms.Automate.Actions;

/// <summary>
/// Output produced by the <see cref="DeleteEntryAction"/>.
/// </summary>
public sealed class DeleteEntryOutput
{
    /// <summary>
    /// Gets the form ID the entry belonged to.
    /// </summary>
    public Guid FormId { get; init; }

    /// <summary>
    /// Gets the unique identifier of the entry.
    /// </summary>
    public Guid RecordUniqueId { get; init; }

    /// <summary>
    /// Gets a value indicating whether an entry was deleted. <c>false</c> when no stored entry
    /// was found (e.g. the form does not store entries, or it was already deleted), or when one
    /// of the form's "on delete" workflows cancelled the deletion.
    /// </summary>
    public bool Deleted { get; init; }
}
