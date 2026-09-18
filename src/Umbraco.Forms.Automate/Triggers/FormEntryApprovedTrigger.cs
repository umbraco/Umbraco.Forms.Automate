using Umbraco.Automate.Core.Triggers;
using Umbraco.Forms.Core.Services.Notifications;

namespace Umbraco.Forms.Automate.Triggers;

/// <summary>
/// Fires when a form entry is approved in Umbraco Forms.
/// Produces one <see cref="TriggerEvent"/> per approved record.
/// </summary>
[Trigger("umbracoForms.formEntryApproved", "Form Entry Approved",
    Description = "Fires when a form entry is approved.",
    Group = "Forms",
    Icon = "icon-check",
    RequiredSections = [Constants.Sections.Forms])]
public sealed class FormEntryApprovedTrigger
    : DynamicFormRecordTriggerBase<RecordApprovedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FormEntryApprovedTrigger"/> class.
    /// </summary>
    public FormEntryApprovedTrigger(TriggerInfrastructure infrastructure, FormFieldResolver resolver)
        : base(infrastructure, resolver)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(RecordApprovedNotification notification)
    {
        yield return CreateEvent(notification.Record, notification.Form);
    }
}
